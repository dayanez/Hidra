using System.Collections.Generic;
using Hidra.Core.Models.Binding;

namespace Hidra.Core.Models
{
    public class DeviceCache
    {

        public string Title { get; set; } = string.Empty;
        public string ProviderName { get; set; } = string.Empty;
        public string DeviceHandle { get; set; } = string.Empty;
        public int DeviceNumber { get; set; }
        public List<DeviceBindingNode> DeviceBindingMenu { get; set; } = new List<DeviceBindingNode>();
        public bool Blockable { get; set; }

    }
}
