using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Hidra.Core.Models.Binding;
using Hidra.Core.Models.Subscription;

namespace Hidra.Core.Models
{
    public class Mapping
    {
        /* Persistence */
        [XmlAttribute]
        public string Title { get; set; } = string.Empty;
        public List<DeviceBinding> DeviceBindings { get; set; } = new List<DeviceBinding>();
        public List<Plugin> Plugins { get; set; } = new List<Plugin>();

        /* Runtime */
        // Profile: null only in the gap between XmlSerializer's parameterless constructor and
        // PostLoad(), same as Profile.Context. InputCache/Multiplexer: null until PrepareMapping
        // runs; Update() (the only reader) is only ever called after that.
        private Profile Profile { get; set; } = null!;
        private List<short> InputCache { get; set; } = null!;
        private List<CallbackMultiplexer> Multiplexer { get; set; } = null!;


        internal bool IsShadowMapping { get; set; }
        internal int ShadowDeviceNumber { get; set; }
        internal int PossibleShadowClones => CountPossibleShadowClones();
        // Set by PrepareMapping, before a live Update() cycle begins.
        internal FilterState FilterState { get; set; } = null!;

        private int CountPossibleShadowClones()
        {
            var usedDeviceConfigurations = new List<DeviceConfiguration>();

            foreach (var deviceBinding in DeviceBindings)
            {
                if (!deviceBinding.IsBound) continue;

                var deviceConfiguration = Profile.GetDeviceConfiguration(DeviceIoType.Input, deviceBinding.DeviceConfigurationGuid);
                if (deviceConfiguration != null) usedDeviceConfigurations.Add(deviceConfiguration);
            }

            if (usedDeviceConfigurations.Count == 0) return 0;

            return usedDeviceConfigurations
                .Select(deviceConfiguration => deviceConfiguration.ShadowDevices)
                .Max(shadowDevices => shadowDevices.Count);
        }

        [XmlIgnore]
        public string FullTitle
        {
            get
            {
                var mapping = GetOverridenMapping();
                return mapping != null ? $"{Title} (Overrides {mapping.Profile.Title})" : Title;
            }
        }

        public Mapping()
        {
            IsShadowMapping = false;
            ShadowDeviceNumber = 0;
        }

        public Mapping(Profile profile, string title) : this()
        {
            Profile = profile;
            Title = title;
        }

        public void Rename(string title)
        {
            Title = title;
            Profile.Context.ContextChanged();
        }

        internal bool IsBound()
        {
            if (DeviceBindings.Count == 0) return false;
            var result = true;
            foreach (var deviceBinding in DeviceBindings)
            {
                result &= deviceBinding.IsBound;
            }
            return result;
        }

        internal void PrepareMapping(FilterState filterState)
        {
            InputCache = new List<short>();
            DeviceBindings.ForEach(_ => InputCache.Add(0));
            Multiplexer = new List<CallbackMultiplexer>();
            for (var i = 0; i < DeviceBindings.Count; i++)
            {
                var cm = new CallbackMultiplexer(InputCache, i, Update);
                Multiplexer.Add(cm);
                DeviceBindings[i].Callback = cm.Update;
                DeviceBindings[i].CurrentValue = 0;
            }

            FilterState = filterState;
            Plugins.ForEach(p => p.RuntimeMapping = this);
        }

        internal Mapping? GetOverridenMapping()
        {
            var list = new List<Mapping>();
            var parentProfile = Profile.ParentProfile;
            if (parentProfile != null) list.AddRange(parentProfile.Mappings);

            while (list.Count > 0)
            {
                var mapping = list[0];
                list.RemoveAt(0);
                if (string.Compare(Title, mapping.Title, StringComparison.CurrentCultureIgnoreCase) == 0)
                {
                    return mapping;
                }

                parentProfile = parentProfile?.ParentProfile;
                if (parentProfile != null) list.AddRange(parentProfile.Mappings);
            }

            return null;
        }

        public void Update(short value)
        {
            foreach (var plugin in Plugins)
            {
                if (plugin.IsFiltered()) continue;

                plugin.Update(InputCache.ToArray());
            }
        }

        #region Plugin

        internal List<Plugin> GetPluginList()
        {
            var plugins = Profile.Context.GetPlugins();
            plugins.Sort();
            if (Plugins.Count > 0)
            {
                plugins = plugins.FindAll(p => p.HasSameInputCategories(Plugins[0]));
            }
            return plugins;
        }

        public bool AddPlugin(Plugin plugin)
        {
            if (Plugins.Count == 0)
            {
                foreach (var _ in plugin.InputCategories)
                {
                    DeviceBindings.Add(new DeviceBinding(Update, Profile, DeviceIoType.Input));
                }
            }

            plugin.SetProfile(Profile);
            Plugins.Add(plugin);

            Profile.Context.ContextChanged();
            return true;
        }

        public bool RemovePlugin(Plugin plugin)
        {
            if (!Plugins.Remove(plugin)) return false;

            if (Plugins.Count == 0)
            {
                DeviceBindings = new List<DeviceBinding>();
            }
            Profile.Context.ContextChanged();

            return true;
        }

        #endregion


        internal Mapping CreateShadowClone(int shadowCloneNumber)
        {
            var clonedMapping = Context.DeepXmlClone<Mapping>(this);
            clonedMapping.Title = $"{clonedMapping.Title} (Shadow {shadowCloneNumber})";
            clonedMapping.IsShadowMapping = true;
            clonedMapping.ShadowDeviceNumber = shadowCloneNumber;
            clonedMapping.Profile = Profile;
            clonedMapping.PostLoad(Profile.Context, Profile);

            foreach (var plugin in clonedMapping.Plugins)
            {
                plugin.Filters.ForEach(f => f.Name = Filter.GetShadowName(f.Name, shadowCloneNumber));
            }

            return clonedMapping;
        }

        internal void PostLoad(Context context, Profile? profile = null)
        {
            // In practice always called with a real profile (Profile.PostLoad's loop always
            // passes 'this'); the default lets PostLoad's own signature stay optional.
            Profile = profile!;
            foreach (var deviceBinding in DeviceBindings)
            {
                deviceBinding.Profile = profile;
            }

            foreach (var plugin in Plugins)
            {
                plugin.PostLoad(context, profile);
            }
        }
    }
}
