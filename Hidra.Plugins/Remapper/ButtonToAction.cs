using Hidra.Core.Attributes;
using Hidra.Core.Models;
using Hidra.Core.Models.Binding;
using Hidra.Plugins.Utilities;

namespace Hidra.Plugins.Remapper
{
    [Plugin("Button to Action", Group = "Action", Description = "Run a program, open a URL, send a key chord, or run a system command (lock, volume, media keys) when a button is pressed or released")]
    [PluginInput(DeviceBindingCategory.Momentary, "Button")]
    public class ButtonToAction : Plugin
    {
        public enum TriggerMode { Press, Release }

        [PluginGui("Trigger on", Order = 0)]
        public TriggerMode Mode { get; set; }

        [PluginGui("Action", Order = 1)]
        public ActionType Type { get; set; }

        [PluginGui("Value", Order = 2)]
        public string Value { get; set; }

        [PluginGui("Arguments (RunProcess only)", Order = 3)]
        public string Arguments { get; set; }

        public ButtonToAction()
        {
            Mode = TriggerMode.Press;
            Type = ActionType.RunProcess;
            Value = string.Empty;
            Arguments = string.Empty;
        }

        public override void Update(params short[] values)
        {
            var triggered = values[0] == 1 && Mode == TriggerMode.Press ||
                             values[0] == 0 && Mode == TriggerMode.Release;
            if (!triggered) return;

            ActionExecutor.ExecuteAsync(Type, Value, Arguments);
        }
    }
}
