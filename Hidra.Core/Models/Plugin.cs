using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Xml.Serialization;
using Hidra.Core.Attributes;
using Hidra.Core.Models.Binding;
using Hidra.Core.Models.Subscription;

namespace Hidra.Core.Models
{
    [InheritedExport(typeof(Plugin))]
    public abstract class Plugin : IComparable<Plugin>
    {
        /* Persistence */
        public List<DeviceBinding> Outputs { get; } = new List<DeviceBinding>();
        public List<Filter> Filters { get; set; } = new List<Filter>();

        /* Runtime */
        // Null for a template instance (e.g. the plugin toolbox) that was never attached to a
        // mapping; ContextChanged() below already treats this defensively.
        internal Profile? Profile { get; set; }
        private List<IODefinition>? _inputCategories;
        private List<IODefinition>? _outputCategories;
        private List<PluginPropertyGroup>? _pluginPropertyGroups;
        // Never actually assigned anywhere in the codebase (Update() logic uses
        // RuntimeMapping.FilterState instead); kept nullable rather than removed here since
        // that's a separate cleanup, not part of this nullable-annotation pass.
        internal FilterState? FilterState { get; set; }
        // Set by Mapping.PrepareMapping before a live Update() cycle begins; every RuntimeMapping
        // read below happens during that cycle.
        internal Mapping RuntimeMapping { get; set; } = null!;

        #region Properties

        [XmlIgnore]
        public List<IODefinition> InputCategories
        {
            get
            {
                if (_inputCategories != null) return _inputCategories;
                _inputCategories = GetIODefinitions(DeviceIoType.Input);
                return _inputCategories;
            }
        }

        [XmlIgnore]
        public List<IODefinition> OutputCategories
        {
            get
            {
                if (_outputCategories != null) return _outputCategories;
                _outputCategories = GetIODefinitions(DeviceIoType.Output);
                return _outputCategories;
            }
        }

        [XmlIgnore]
        public List<PluginPropertyGroup> PluginPropertyGroups
        {
            get
            {
                if (_pluginPropertyGroups != null) return _pluginPropertyGroups;
                _pluginPropertyGroups = GetGuiMatrix();
                return _pluginPropertyGroups;
            }
        }

        [XmlIgnore]
        public string PluginName => GetPluginAttribute().Name;
        [XmlIgnore]
        public string Description => GetPluginAttribute().Description ?? string.Empty;
        [XmlIgnore]
        public string Group => GetPluginAttribute().Group ?? string.Empty;
        [XmlIgnore]
        public bool IsDisabled => GetPluginAttribute().Disabled;

        #endregion

        public struct IODefinition
        {
            public string Name;
            public DeviceBindingCategory Category;
            public string? GroupName;
        }

        public enum FilterMode
        {
            Active,
            Inactive,
            Toggle,
            Unchanged
        }

        protected Plugin()
        {
            foreach (var _ in OutputCategories)
            {
                Outputs.Add(new DeviceBinding(null, Profile, DeviceIoType.Output));
            }
        }

        #region Life cycle

        public virtual void OnActivate()
        {

        }

        public virtual void OnPropertyChanged()
        {

        }

        /*
         * Called before a plugin OnActivate and OnPropertyChanged to cache run-time values
         */
        public virtual void InitializeCacheValues()
        {

        }

        public virtual void Update(params short[] values)
        {

        }

        public virtual void OnDeactivate()
        {

        }

        #endregion

        #region Plugin methods

        protected void WriteOutput(int number, short value)
        {
            Outputs[number].WriteOutput(value);
        }

        protected long ReadOutput(int number)
        {
            return Outputs[number].CurrentValue;
        }

        protected void WriteFilterState(string filterName, bool value)
        {
            RuntimeMapping.FilterState.SetFilterState(GetFilterName(filterName), value);
        }

        protected void ToggleFilterState(string filterName)
        {
            RuntimeMapping.FilterState.ToggleFilterState(GetFilterName(filterName));
        }

        private string GetFilterName(string filterName)
        {
            var filter = filterName.ToLower();
            return RuntimeMapping.IsShadowMapping
                ? Filter.GetShadowName(filter, RuntimeMapping.ShadowDeviceNumber)
                : filter;
        }

        #endregion

        #region Filters

        internal Filter AddFilter(string name, bool negative = false)
        {
            var existingFilter = Filters.Find(f => string.Equals(f.Name, name, StringComparison.CurrentCultureIgnoreCase));

            if (existingFilter != null)
            {
                existingFilter.Negative = negative;
                ContextChanged();
                return existingFilter;
            }

            var filter = new Filter()
            {
                Name = name,
                Negative = negative
            };

            Filters.Add(filter);
            ContextChanged();
            return filter;
        }

        internal bool RemoveFilter(Filter filter)
        {
            var success = Filters.Remove(filter);
            if (success) ContextChanged();
            return success;
        }

        internal void ToggleFilter(Filter filter)
        {
            var existingFilter = Filters.Find(f => string.Equals(f.Name, filter.Name, StringComparison.InvariantCultureIgnoreCase));
            if (existingFilter != null) AddFilter(existingFilter.Name, !existingFilter.Negative);
        }

        #endregion

        public void SetProfile(Profile? profile)
        {
            Profile = profile;
            Outputs.ForEach(o => o.Profile = profile);
        }

        public Plugin Duplicate()
        {
            var newPlugin = Context.DeepXmlClone(this);
            // Duplicate() is only ever called on a plugin that's already part of a mapping, so
            // Profile is already set at this point.
            newPlugin.PostLoad(Profile!.Context, Profile);
            return newPlugin;
        }

        public void ContextChanged()
        {
            Profile?.Context?.ContextChanged();
        }

        #region Loading

        public void PostLoad(Context context, Profile? parentProfile)
        {
            SetProfile(parentProfile);
            ZipDeviceBindingList(Outputs);
            Outputs.ForEach(o => o.DeviceIoType = DeviceIoType.Output);

        }

        private static void ZipDeviceBindingList(IList<DeviceBinding> deviceBindings)
        {
            if (deviceBindings.Count == 0) return;
            var split = deviceBindings.Count / 2;
            for (var i = 0; i < split; i++)
            {
                deviceBindings[i].IsBound = deviceBindings[i + split].IsBound;
                deviceBindings[i].DeviceConfigurationGuid = deviceBindings[i + split].DeviceConfigurationGuid;
                deviceBindings[i].KeyType = deviceBindings[i + split].KeyType;
                deviceBindings[i].KeyValue = deviceBindings[i + split].KeyValue;
                deviceBindings[i].KeySubValue = deviceBindings[i + split].KeySubValue;
            }

            for (var i = deviceBindings.Count - 1; i >= split; i--)
            {
                deviceBindings.Remove(deviceBindings[i]);
            }
        }

        #endregion

        #region Comparison

        public int CompareTo(Plugin? other)
        {
            return string.Compare(PluginName, other?.PluginName, StringComparison.Ordinal);
        }

        public bool HasSameInputCategories(Plugin other)
        {
            if (InputCategories.Count > other.InputCategories.Count) return false;
            for (var i = 0; i < InputCategories.Count; i++)
            {
                if (InputCategories[i].Category != other.InputCategories[i].Category) return false;
            }

            return true;
        }

        #endregion

        #region Attributes

        private PluginAttribute GetPluginAttribute()
        {
            var pluginAttribute = (PluginAttribute?)Attribute.GetCustomAttribute(GetType(), typeof(PluginAttribute));

            return pluginAttribute ?? new PluginAttribute("Invalid plugin");
        }

        private List<IODefinition> GetIODefinitions(DeviceIoType deviceIoType)
        {
            var attributes = (PluginIoAttribute[])Attribute.GetCustomAttributes(GetType(), typeof(PluginIoAttribute));

            return attributes.Where(a => a.DeviceIoType == deviceIoType).Select(a => new IODefinition()
            {
                Category = a.DeviceBindingCategory,
                Name = a.Name,
                GroupName = a.Group
            }).ToList();
        }

        private List<PluginProperty> GetGuiProperties()
        {
            var properties = from p in GetType().GetProperties()
                             let attr = p.GetCustomAttributes(typeof(PluginGuiAttribute), true)
                             where attr.Length == 1
                             // Safe: GetCustomAttributes was already filtered to PluginGuiAttribute above.
                             select new { Property = p, Attribute = (PluginGuiAttribute)attr.First() };


            return properties.Select(prop => new PluginProperty(this, prop.Property, prop.Attribute.Name, prop.Attribute.Order, prop.Attribute.Group)).ToList();
        }

        private List<PluginGroupAttribute> GetPluginGroups()
        {
            return GetType().GetCustomAttributes(typeof(PluginGroupAttribute), true).ToList()
                .Select(a => ((PluginGroupAttribute)a)).ToList();
        }

        private List<string> GetPluginOutputGroups()
        {
            return GetType().GetCustomAttributes(typeof(PluginOutput), true).ToList()
                .Select(a => ((PluginOutput)a).Group)
                .Where(group => group != null)
                .Select(group => group!)
                .Distinct().ToList();
        }

        public List<PluginPropertyGroup> GetGuiMatrix()
        {
            var result = new List<PluginPropertyGroup>();

            var guiProperties = GetGuiProperties();
            guiProperties.Sort();

            var ungroupedProperties = guiProperties.FindAll(p => p.Group == null);
            if (ungroupedProperties.Count > 0)
            {
                result.Add(new PluginPropertyGroup()
                {
                    Title = "Settings",
                    GroupName = "Settings",
                    GroupType = PluginPropertyGroup.GroupTypes.Settings,
                    PluginProperties = ungroupedProperties
                });
            }

            foreach (var group in GetPluginGroups())
            {
                if (group.Group == null) continue;

                var properties = guiProperties.FindAll(p => group.Group.Equals(p.Group));
                result.Add(new PluginPropertyGroup()
                {
                    Title = group.Name,
                    GroupName = group.Group,
                    GroupType = GetPluginOutputGroups().Contains(group.Group)
                        ? PluginPropertyGroup.GroupTypes.Output
                        : PluginPropertyGroup.GroupTypes.Settings,
                    PluginProperties = properties
                });
            }

            foreach (var pluginGroup in result)
            {
                pluginGroup.PluginProperties.Sort((x, y) => x.Order.CompareTo(y.Order));
            }

            return result;
        }

        #endregion

        public virtual PropertyValidationResult Validate(PropertyInfo propertyInfo, dynamic value)
        {
            return PropertyValidationResult.ValidResult;
        }

        public bool IsFiltered()
        {
            if (Filters.Count == 0) return false;

            foreach (var filter in Filters)
            {
                RuntimeMapping.FilterState.FilterRuntimeDictionary.TryGetValue(filter.Name.ToLower(), out var filterValue);
                if (!(filterValue ^ filter.Negative)) return true;
            }

            return false;
        }
    }
}
