using System.Reflection;
using Hidra.Core.Attributes;
using Hidra.Core.Models;
using Hidra.Core.Models.Binding;
using Hidra.Core.Utilities;
using Hidra.Core.Utilities.AxisHelpers;

namespace Hidra.Plugins.Remapper
{
    [Plugin("Axis to Axis (Modifier)", Group = "Axis", Description = "Map from one axis to another, with a held button from any device (e.g. a keyboard key) switching to an alternate sensitivity - a cross-device 'sniper mode' / DPI preset")]
    [PluginInput(DeviceBindingCategory.Range, "Axis")]
    [PluginInput(DeviceBindingCategory.Momentary, "Modifier")]
    [PluginOutput(DeviceBindingCategory.Range, "Axis")]
    [PluginSettingsGroup("Sensitivity", Group = "Sensitivity")]
    [PluginSettingsGroup("Dead zone", Group = "Dead zone")]
    public class AxisToAxisWithModifier : Plugin
    {
        [PluginGui("Invert")]
        public bool Invert { get; set; }

        [PluginGui("Linear", Group = "Sensitivity", Order = 2)]
        public bool Linear { get; set; }

        [PluginGui("Percentage", Group = "Dead zone", Order = 0)]
        public int DeadZone { get; set; }

        [PluginGui("Anti-dead zone", Group = "Dead zone")]
        public int AntiDeadZone { get; set; }

        [PluginGui("Percentage", Group = "Sensitivity", Order = 0)]
        public int Sensitivity { get; set; }

        [PluginGui("Modifier held percentage", Group = "Sensitivity", Order = 1)]
        public int ModifierSensitivity { get; set; }

        private readonly DeadZoneHelper _deadZoneHelper = new DeadZoneHelper();
        private readonly AntiDeadZoneHelper _antiDeadZoneHelper = new AntiDeadZoneHelper();
        private readonly SensitivityHelper _sensitivityHelper = new SensitivityHelper();
        private readonly SensitivityHelper _modifierSensitivityHelper = new SensitivityHelper();

        public AxisToAxisWithModifier()
        {
            DeadZone = 0;
            AntiDeadZone = 0;
            Sensitivity = 100;
            ModifierSensitivity = 50;
        }

        public override void InitializeCacheValues()
        {
            Initialize();
        }

        public override void Update(params short[] values)
        {
            var value = values[0];
            var modifierHeld = values[1] != 0;

            if (Invert) value = Functions.Invert(value);
            if (DeadZone != 0) value = _deadZoneHelper.ApplyRangeDeadZone(value);
            if (AntiDeadZone != 0) value = _antiDeadZoneHelper.ApplyRangeAntiDeadZone(value);

            if (modifierHeld)
            {
                if (ModifierSensitivity != 100) value = _modifierSensitivityHelper.ApplyRangeSensitivity(value);
            }
            else
            {
                if (Sensitivity != 100) value = _sensitivityHelper.ApplyRangeSensitivity(value);
            }

            WriteOutput(0, value);
        }

        private void Initialize()
        {
            _deadZoneHelper.Percentage = DeadZone;
            _antiDeadZoneHelper.Percentage = AntiDeadZone;
            _sensitivityHelper.Percentage = Sensitivity;
            _sensitivityHelper.IsLinear = Linear;
            _modifierSensitivityHelper.Percentage = ModifierSensitivity;
            _modifierSensitivityHelper.IsLinear = Linear;
        }

        public override PropertyValidationResult Validate(PropertyInfo propertyInfo, dynamic value)
        {
            switch (propertyInfo.Name)
            {
                case nameof(DeadZone):
                case nameof(AntiDeadZone):
                    return InputValidation.ValidatePercentage(value);
            }

            return PropertyValidationResult.ValidResult;
        }
    }
}
