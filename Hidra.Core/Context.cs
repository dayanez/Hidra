using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Hidra.IOWrapper.Core;
using Hidra.Core.Annotations;
using Hidra.Core.Managers;
using Hidra.Core.Models;
using Mono.Options;
using NLog;

namespace Hidra.Core
{
    public sealed class Context : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static string ContextName => Path.Combine(AppContext.BaseDirectory, "context.xml");
        private const string PluginPath = "Plugins";

        /* Persistence */
        public List<Profile> Profiles { get; set; } = new List<Profile>();

        /* Runtime */
        [XmlIgnore] public Profile? ActiveProfile { get; set; }
        [XmlIgnore] public ProfilesManager ProfilesManager { get; set; } = null!;
        [XmlIgnore] public DevicesManager DevicesManager { get; set; } = null!;
        [XmlIgnore] public SubscriptionsManager SubscriptionsManager { get; set; } = null!;
        [XmlIgnore] public PluginsManager PluginManager { get; set; } = null!;
        [XmlIgnore] public BindingManager BindingManager { get; set; } = null!;
        [XmlIgnore] public ProcessProfileSwitcher ProcessProfileSwitcher { get; set; } = null!;

        public delegate void ActiveProfileChanged(Profile? profile);
        public event ActiveProfileChanged? ActiveProfileChangedEvent;

        internal bool IsNotSaved { get; private set; }
        // Left unset (rather than nullable) if the try/catch below hits DirectoryNotFoundException;
        // that's a pre-existing startup-failure condition this nullable pass doesn't change the
        // behavior of, since every consumer already calls into it unconditionally.
        internal IOController IOController { get; set; } = null!;
        private OptionSet options = null!;

        public Context()
        {
            Init();
            SetCommandLineOptions();
        }

        [MemberNotNull(nameof(Profiles), nameof(ProfilesManager), nameof(DevicesManager),
            nameof(SubscriptionsManager), nameof(PluginManager), nameof(BindingManager), nameof(ProcessProfileSwitcher))]
        private void Init()
        {
            IsNotSaved = false;
            Profiles = new List<Profile>();

            try
            {
                IOController = new IOController();
            }
            catch (DirectoryNotFoundException e)
            {
                Logger.Error("IOWrapper provider directory not found", e);
            }

            ProfilesManager = new ProfilesManager(this, Profiles);
            DevicesManager = new DevicesManager(this);
            SubscriptionsManager = new SubscriptionsManager(this);
            PluginManager = new PluginsManager(PluginPath);
            BindingManager = new BindingManager(this);
            ProcessProfileSwitcher = new ProcessProfileSwitcher(this);
        }

        [MemberNotNull(nameof(options))]
        private void SetCommandLineOptions()
        {
            options = new OptionSet {
                { "p|profile=", "The profile to search for", FindAndLoadProfile }
            };
        }

        private void FindAndLoadProfile(string profileString)
        {
            Logger.Debug($"Searching for profile to load: {{{profileString}}}");
            var search = profileString.Split(',').ToList();
            var profile = ProfilesManager.FindProfile(search);
            if (profile != null) SubscriptionsManager.ActivateProfile(profile);
        }

        public void ParseCommandLineArguments(IEnumerable<string> args)
        {
            options.Parse(args);
        }

        public List<Plugin> GetPlugins()
        {
            return PluginManager.Plugins.Where(p => !p.IsDisabled).ToList();
        }

        public void ContextChanged()
        {
            Logger.Trace("Context changed");
            IsNotSaved = true;
        }

        #region Persistence

        public bool SaveContext(List<Type>? pluginTypes = null)
        {
            var serializer = GetXmlSerializer(pluginTypes);
            using (var streamWriter = new StreamWriter(ContextName))
            {
                serializer.Serialize(streamWriter, this);
            }
            IsNotSaved = false;

            return true;
        }

        public static Context Load(List<Type>? pluginTypes = null)
        {
            Context context;
            var serializer = GetXmlSerializer(pluginTypes);
            try
            {
                using (var fileStream = new FileStream(ContextName, FileMode.Open))
                {
                    // A successful Deserialize() of a context.xml written by SaveContext always
                    // yields a Context; XmlSerializer's return type is just object-shaped.
                    context = (Context)serializer.Deserialize(fileStream)!;
                    context.PostLoad();
                }
            }
            catch (IOException e)
            {
                Logger.Error("Failed to load context.xml", e);
                context = new Context();
            }
            return context;
        }

        private void PostLoad()
        {
            foreach (var profile in Profiles)
            {
                profile.PostLoad(this);
            }
        }

        private static XmlSerializer GetXmlSerializer(List<Type>? additionalPluginTypes)
        {
            return GetXmlSerializer(additionalPluginTypes, typeof(Context));
        }

        private static XmlSerializer GetXmlSerializer(List<Type>? additionalPluginTypes, Type type)
        {
            var plugins = new PluginsManager(PluginPath);
            var pluginTypes = plugins.Plugins.Select(p => p.GetType()).ToList();
            if (additionalPluginTypes != null) pluginTypes.AddRange(additionalPluginTypes);
            return new XmlSerializer(type, pluginTypes.ToArray());
        }

        #endregion

        public void Dispose()
        {
            ProcessProfileSwitcher?.Dispose();
            SubscriptionsManager.Dispose();
            IOController?.Dispose();
        }

        public static T DeepXmlClone<T>(T obj)
        {
            using (var ms = new MemoryStream())
            {
                var formatter = GetXmlSerializer(null, typeof(T));
                formatter.Serialize(ms, obj);
                ms.Position = 0;

                // Same reasoning as Load() above: a round-trip of a just-serialized T always
                // deserializes back to a T.
                return (T)formatter.Deserialize(ms)!;
            }
        }

        public void OnActiveProfileChangedEvent(Profile? profile)
        {
            ActiveProfileChangedEvent?.Invoke(profile);
        }
    }
}
