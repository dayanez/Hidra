using Hidra.Core.Models;
using Hidra.Core.Models.Binding;

namespace Hidra.Core.Attributes
{
    public class PluginInput : PluginIoAttribute
    {
        public PluginInput(DeviceBindingCategory deviceBindingCategory, string name) : base(DeviceIoType.Input, deviceBindingCategory, name)
        {
        }
    }
}
