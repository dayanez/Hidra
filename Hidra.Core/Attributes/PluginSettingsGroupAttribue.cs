using System;

namespace Hidra.Core.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class PluginSettingsGroupAttribute : PluginGroupAttribute
    {
        public PluginSettingsGroupAttribute(string name)
        {
            Name = name;
        }

        public override string Name { get; set; }
    }
}