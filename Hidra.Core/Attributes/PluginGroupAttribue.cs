using System;

namespace Hidra.Core.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class PluginGroupAttribute : Attribute
    {
        public virtual string Name { get; set; } = string.Empty;
        public string? Group { get; set; }

        public PluginGroupAttribute()
        {
        }
    }
}
