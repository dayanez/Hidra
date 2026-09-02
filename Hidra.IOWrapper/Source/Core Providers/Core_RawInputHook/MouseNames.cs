using System.Collections.Generic;

namespace Core_RawInputHook
{
    /// <summary>
    /// Friendly names for mouse buttons, indexed the way Core_RawInputHook reports/expects them.
    /// </summary>
    internal static class MouseNames
    {
        public static readonly List<string> ButtonNames = new List<string>
        {
            "Left Mouse", "Right Mouse", "Middle Mouse", "Side Button 1", "Side Button 2",
            "Wheel Up", "Wheel Down", "Wheel Right", "Wheel Left"
        };

        public const int LeftButton = 0;
        public const int RightButton = 1;
        public const int MiddleButton = 2;
        public const int XButton1 = 3;
        public const int XButton2 = 4;
        public const int WheelUp = 5;
        public const int WheelDown = 6;
        public const int WheelRight = 7;
        public const int WheelLeft = 8;

        public const int AxisX = 0;
        public const int AxisY = 1;
    }
}
