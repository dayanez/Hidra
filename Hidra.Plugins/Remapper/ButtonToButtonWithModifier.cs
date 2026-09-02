using Hidra.Core.Attributes;
using Hidra.Core.Models;
using Hidra.Core.Models.Binding;

namespace Hidra.Plugins.Remapper
{
    [Plugin("Button to Button (Chord)", Group = "Button", Description = "Map one button to another, but only while a modifier button from any device is held (or not held) - a cross-device chord, e.g. a keyboard key changing what a mouse button does")]
    [PluginInput(DeviceBindingCategory.Momentary, "Button")]
    [PluginInput(DeviceBindingCategory.Momentary, "Modifier")]
    [PluginOutput(DeviceBindingCategory.Momentary, "Button")]
    public class ButtonToButtonWithModifier : Plugin
    {
        [PluginGui("Invert output")]
        public bool Invert { get; set; }

        [PluginGui("Modifier must be held")]
        public bool ModifierMustBeHeld { get; set; }

        public ButtonToButtonWithModifier()
        {
            ModifierMustBeHeld = true;
        }

        public override void Update(params short[] values)
        {
            var buttonPressed = values[0] != 0;
            var modifierHeld = values[1] != 0;

            var gateOpen = modifierHeld == ModifierMustBeHeld;
            var outputPressed = buttonPressed && gateOpen;
            if (Invert) outputPressed = !outputPressed;

            WriteOutput(0, (short)(outputPressed ? 1 : 0));
        }
    }
}
