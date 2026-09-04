using System;
using System.Collections.Generic;
using System.Linq;
using Hidra.Core.Models;
using NLog;

namespace Hidra.Core.Managers
{
    public class ProfilesManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly Context _context;
        private readonly List<Profile> _profiles;

        public ProfilesManager(Context context, List<Profile> profiles)
        {
            _context = context;
            _profiles = profiles;
        }

        public Profile CreateProfile(string title, List<DeviceConfiguration> inputDevices, List<DeviceConfiguration> outputDevices)
        {
            return Profile.CreateProfile(_context, title, inputDevices, outputDevices);
        }

        public bool AddProfile(Profile newProfile, Profile? parentProfile = null)
        {
            if (parentProfile != null)
            {
                parentProfile.AddChildProfile(newProfile);
            }
            else
            {
                _profiles.Add(newProfile);
            }

            _context.ContextChanged();
            return true;
        }

        public bool CopyProfile(Profile profile, string title = "Untitled")
        {
            var newProfile = Context.DeepXmlClone<Profile>(profile);
            newProfile.Title = title;
            RemapGuids(newProfile);
            newProfile.PostLoad(_context, profile.ParentProfile);

            if (profile.ParentProfile != null)
            {
                profile.ParentProfile.AddChildProfile(newProfile);
            }
            else
            {
                _profiles.Add(newProfile);
            }

            _context.ContextChanged();

            return true;
        }

        // The XML clone preserves every persisted Guid verbatim, so without this the copy would
        // carry the same Profile.Guid and DeviceConfiguration.Guid values as the original
        // throughout the whole copied subtree (including nested child profiles). That's not just
        // cosmetic: Profile.IsActive() matches by Guid rather than by reference, so a duplicated
        // child profile would report itself active whenever its original counterpart is. Device
        // bindings reference their device configuration by Guid too, so each configuration's new
        // Guid is tracked and every binding pointing at it (anywhere in the copied subtree) is
        // rewritten to match; a binding that instead points at a configuration on an ancestor
        // profile (outside the copied subtree) is left alone, since that configuration was never
        // duplicated.
        private static void RemapGuids(Profile profile)
        {
            var configurationGuidMap = new Dictionary<Guid, Guid>();
            RemapProfileAndConfigurationGuids(profile, configurationGuidMap);
            RemapDeviceBindingGuids(profile, configurationGuidMap);
        }

        private static void RemapProfileAndConfigurationGuids(Profile profile, Dictionary<Guid, Guid> configurationGuidMap)
        {
            profile.Guid = Guid.NewGuid();

            foreach (var configuration in profile.InputDeviceConfigurations.Concat(profile.OutputDeviceConfigurations))
            {
                var newGuid = Guid.NewGuid();
                configurationGuidMap[configuration.Guid] = newGuid;
                configuration.Guid = newGuid;
            }

            profile.ChildProfiles.ForEach(childProfile => RemapProfileAndConfigurationGuids(childProfile, configurationGuidMap));
        }

        private static void RemapDeviceBindingGuids(Profile profile, Dictionary<Guid, Guid> configurationGuidMap)
        {
            // Mapping.DeviceBindings holds the input side; each plugin's own Outputs list (not
            // Mapping.DeviceBindings) holds the output side, and needs remapping too.
            var inputBindings = profile.Mappings.SelectMany(mapping => mapping.DeviceBindings);
            var outputBindings = profile.Mappings.SelectMany(mapping => mapping.Plugins).SelectMany(plugin => plugin.Outputs);

            foreach (var deviceBinding in inputBindings.Concat(outputBindings))
            {
                if (configurationGuidMap.TryGetValue(deviceBinding.DeviceConfigurationGuid, out var newGuid))
                {
                    deviceBinding.DeviceConfigurationGuid = newGuid;
                }
            }

            profile.ChildProfiles.ForEach(childProfile => RemapDeviceBindingGuids(childProfile, configurationGuidMap));
        }

        /// <summary>
        /// Breadth-first search for nested profiles
        /// Find first search result and looks for the next result in the children
        /// </summary>
        /// <param name="search">List of profiles to search for nested under each other</param>
        /// <returns>The most specific profile found in the chain, otherwise null</returns>
        public Profile? FindProfile(List<string> search)
        {
            Logger.Debug($"Searching for profile: {{{string.Join(",", search)}}}");
            Profile? foundProfile = null;
            if (search.Count == 0) return null;
            var queue = new List<Profile>();
            queue.AddRange(_profiles);
            while (queue.Count > 0)
            {
                var profile = queue[0];
                queue.RemoveAt(0);
                if (profile.Title.ToLower().Equals(search.First().ToLower()))
                {
                    if (search.Count == 1)
                    {
                        Logger.Debug($"Found profile: {{{profile.ProfileBreadCrumbs()}}}");
                        return profile;
                    }
                    foundProfile = profile;
                    search.RemoveAt(0);
                    Logger.Trace($"Found intermediate profile: {{{profile.ProfileBreadCrumbs()}}}. Remaining search: {{{string.Join(",", search)}}}");
                    queue.Clear();
                }
                if (profile.ChildProfiles != null) queue.AddRange(profile.ChildProfiles);

            }
            if (foundProfile == null) Logger.Debug($"No profile found for {{{string.Join(",", search)}}}");
            return foundProfile;
        }
    }
}
