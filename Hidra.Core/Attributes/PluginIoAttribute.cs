using System;
using Hidra.Core.Models;
using Hidra.Core.Models.Binding;

namespace Hidra.Core.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class PluginIoAttribute : PluginGroupAttribute
    {
        public virtual DeviceIoType DeviceIoType { get; }
        public virtual DeviceBindingCategory DeviceBindingCategory { get; }
        public override string Name { get; set; }

        public PluginIoAttribute(DeviceIoType deviceIoType, DeviceBindingCategory deviceBindingCategory, string name)
        {
            DeviceIoType = deviceIoType;
            DeviceBindingCategory = deviceBindingCategory;
            Name = name;
        }
    }
}
