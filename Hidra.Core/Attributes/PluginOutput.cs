using Hidra.Core.Models;
using Hidra.Core.Models.Binding;

namespace Hidra.Core.Attributes
{
    public class PluginOutput : PluginIoAttribute
    {
        public PluginOutput(DeviceBindingCategory deviceBindingCategory, string name) : base(DeviceIoType.Output, deviceBindingCategory, name)
        {
        }
    }
}
