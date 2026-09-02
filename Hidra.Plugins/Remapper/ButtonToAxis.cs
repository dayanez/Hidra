using System.Reflection;
using Hidra.Core.Attributes;
using Hidra.Core.Models;
using Hidra.Core.Models.Binding;
using Hidra.Core.Utilities;

namespace Hidra.Plugins.Remapper
{
    [Plugin("Button to Axis", Group = "Axis", Description = "Map from one button to different axis values")]
    [PluginInput(DeviceBindingCategory.Momentary, "Button")]
    [PluginOutput(DeviceBindingCategory.Range, "Axis", Group = "Axis")]
    public class ButtonToAxis : Plugin
    {
        [PluginGui("Axis on release", Order = 0, Group = "Axis")] 
        public double Range { get; set; }

        [PluginGui("Initialize axis", Order = 0)]
        public bool Initialize { get; set; }

        [PluginGui("Axis when pressed", Order = 1, Group = "Axis")]
        public double RangePressed { get; set; }

        public ButtonToAxis()
        {
            Range = 0;
            RangePressed = 100;
        }

        public override void OnActivate()
        {
            if (Initialize) WriteOutput(0, Functions.GetRangeFromPercentage((short)Range));
        }

        public override void Update(params short[] values)
        {
            WriteOutput(0,
                values[0] == 0
                    ? Functions.GetRangeFromPercentage((short)Range)
                    : Functions.GetRangeFromPercentage((short)RangePressed));
        }

        public override PropertyValidationResult Validate(PropertyInfo propertyInfo, dynamic value)
        {
            switch (propertyInfo.Name)
            {
                case nameof(Range):
                case nameof(RangePressed):
                    return InputValidation.ValidateRange(value, -100.0, 100.0);
            }

            return PropertyValidationResult.ValidResult;
        }
    }
}
