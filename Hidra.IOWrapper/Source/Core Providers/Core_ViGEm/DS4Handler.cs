using System;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;
using System.Collections.Generic;
using HidWizards.IOWrapper.DataTransferObjects;

namespace Core_ViGEm
{
    public partial class Core_ViGEm
    //public class Core_ViGEm
    {
        /// <summary>
        /// Handler for an individual DS4 controller
        /// </summary>
        private class DS4Handler : DeviceHandler
        {
            private static readonly List<DualShock4Axis> AxisIndexes = new List<DualShock4Axis>
            {
                DualShock4Axis.LeftThumbX, DualShock4Axis.LeftThumbY, DualShock4Axis.RightThumbX, DualShock4Axis.RightThumbY
            };

            private static readonly List<DualShock4Slider> SliderIndexes = new List<DualShock4Slider>
            {
                DualShock4Slider.LeftTrigger, DualShock4Slider.RightTrigger
            };

            protected override List<string> axisNames { get; set; } = new List<string>
            {
                "LX", "LY", "RX", "RY", "L2 (LT)", "R2 (RT)"
            };

            private static readonly List<DualShock4Button> ButtonIndexes = new List<DualShock4Button>
            {
                DualShock4Button.Cross, DualShock4Button.Circle, DualShock4Button.Square, DualShock4Button.Triangle,
                DualShock4Button.ShoulderLeft, DualShock4Button.ShoulderRight, DualShock4Button.ThumbLeft, DualShock4Button.ThumbRight,
                DualShock4Button.Share, DualShock4Button.Options,
                DualShock4Button.TriggerLeft, DualShock4Button.TriggerRight
            };

            // Bit values within the special-buttons byte passed to SetSpecialButtonsFull
            private static readonly List<int> SpecialButtonBits = new List<int>
            {
                DualShock4SpecialButton.Ps.Value, DualShock4SpecialButton.Touchpad.Value
            };

            private byte _specialButtonState;

            protected override List<string> buttonNames { get; set; } = new List<string>
            {
                "Cross", "Circle", "Square", "Triangle", "L1", "R1", "LS", "RS", "Share", "Options", "L2", "R2", "PS", "TouchPad Click"
            };

            private static readonly List<DualShock4DPadDirection> PovIndexes = new List<DualShock4DPadDirection>
            {
                DualShock4DPadDirection.North, DualShock4DPadDirection.East, DualShock4DPadDirection.South, DualShock4DPadDirection.West
            };

            private readonly Dictionary<string, int> _povAxisStates = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                {"x", 0 }, {"y", 0}
            };

            private static readonly Dictionary<int, (string Axis, int Direction)> IndexToVector = new Dictionary<int, (string, int)>()
            {
                {0, ("y", -1)},
                {1, ("x", 1)},
                {2, ("y", 1)},
                {3, ("x", -1)}
            };

            private static readonly Dictionary<(int x, int y), DualShock4DPadDirection> AxisStatesToDpadValue = new Dictionary<(int x, int y), DualShock4DPadDirection>()
            {
                {(0, 0), DualShock4DPadDirection.None},
                {(0, -1), DualShock4DPadDirection.North},
                {(1, -1), DualShock4DPadDirection.Northeast},
                {(1, 0), DualShock4DPadDirection.East},
                {(1, 1), DualShock4DPadDirection.Southeast},
                {(0, 1), DualShock4DPadDirection.South},
                {(-1, 1), DualShock4DPadDirection.Southwest},
                {(-1, 0), DualShock4DPadDirection.West},
                {(-1, -1), DualShock4DPadDirection.Northwest}
            };

            public DS4Handler(DeviceClassDescriptor descriptor, int index) : base(descriptor, index)
            {
            }

            protected override void AcquireTarget()
            {
                target = _client.CreateDualShock4Controller();
                target.Connect();
                target.AutoSubmitReport = true;
            }

            protected override void RelinquishTarget()
            {
                target.Disconnect();
                target = null;
            }

            protected override void SetAxisState(BindingDescriptor bindingDescriptor, int state)
            {
                var inputId = bindingDescriptor.Index;
                var ds4 = (IDualShock4Controller)target;
                var byteState = (byte)((state + 32768) / 256);
                if (inputId > 3)
                {
                    ds4.SetSliderValue(SliderIndexes[inputId - 4], byteState);
                }
                else
                {
                    ds4.SetAxisValue(AxisIndexes[inputId], byteState);
                }
            }

            protected override void SetButtonState(BindingDescriptor bindingDescriptor, int state)
            {
                var inputId = bindingDescriptor.Index;
                var ds4 = (IDualShock4Controller)target;
                if (inputId >= ButtonIndexes.Count)
                {
                    var bit = SpecialButtonBits[inputId - ButtonIndexes.Count];
                    _specialButtonState = (byte)(state != 0
                        ? _specialButtonState | bit
                        : _specialButtonState & ~bit);
                    ds4.SetSpecialButtonsFull(_specialButtonState);
                }
                else
                {
                    ds4.SetButtonState(ButtonIndexes[inputId], state != 0);
                }
            }

            protected override void SetPovState(BindingDescriptor bindingDescriptor, int state)
            {
                var inputId = bindingDescriptor.Index;
                var mapping = IndexToVector[inputId];
                var axisState = _povAxisStates[mapping.Axis];
                var newState = state == 1 ? mapping.Direction : 0;
                if (axisState == newState) return;
                _povAxisStates[mapping.Axis] = newState;

                var direction = AxisStatesToDpadValue[(_povAxisStates["x"], _povAxisStates["y"])];
                ((IDualShock4Controller)target).SetDPadDirection(direction);
            }
        }
    }
}
