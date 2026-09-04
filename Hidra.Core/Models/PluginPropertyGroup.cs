using System.Collections.Generic;

namespace Hidra.Core.Models
{
    public class PluginPropertyGroup
    {
        public string Title { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public List<PluginProperty> PluginProperties { get; set; } = new List<PluginProperty>();
        public GroupTypes GroupType { get; set; }

        public enum GroupTypes
        {
            Settings,
            Output
        }
    }
}
