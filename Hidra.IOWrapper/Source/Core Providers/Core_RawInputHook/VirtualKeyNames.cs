using System.Collections.Generic;

namespace Core_RawInputHook
{
    /// <summary>
    /// Friendly names for Windows virtual-key codes (VK_*), used as the binding Index for keyboard
    /// buttons on this provider. Covers the keys a remapper is realistically going to be bound to;
    /// anything missing falls back to "Key 0xNN" in <see cref="KeyboardDeviceLibrary"/>.
    /// </summary>
    internal static class VirtualKeyNames
    {
        public static readonly Dictionary<int, string> Names = BuildNames();

        private static Dictionary<int, string> BuildNames()
        {
            var names = new Dictionary<int, string>
            {
                { 0x08, "Backspace" }, { 0x09, "Tab" }, { 0x0D, "Enter" },
                { 0x10, "Shift" }, { 0x11, "Ctrl" }, { 0x12, "Alt" }, { 0x13, "Pause" },
                { 0x14, "Caps Lock" }, { 0x1B, "Esc" }, { 0x20, "Space" },
                { 0x21, "Page Up" }, { 0x22, "Page Down" }, { 0x23, "End" }, { 0x24, "Home" },
                { 0x25, "Left" }, { 0x26, "Up" }, { 0x27, "Right" }, { 0x28, "Down" },
                { 0x2C, "Print Screen" }, { 0x2D, "Insert" }, { 0x2E, "Delete" },
                { 0x5B, "Left Windows" }, { 0x5C, "Right Windows" }, { 0x5D, "Menu" },
                { 0x90, "Num Lock" }, { 0x91, "Scroll Lock" },
                { 0xA0, "Left Shift" }, { 0xA1, "Right Shift" },
                { 0xA2, "Left Ctrl" }, { 0xA3, "Right Ctrl" },
                { 0xA4, "Left Alt" }, { 0xA5, "Right Alt" },
                { 0xBA, "; :" }, { 0xBB, "= +" }, { 0xBC, ", <" }, { 0xBD, "- _" },
                { 0xBE, ". >" }, { 0xBF, "/ ?" }, { 0xC0, "` ~" },
                { 0xDB, "[ {" }, { 0xDC, "\\ |" }, { 0xDD, "] }" }, { 0xDE, "' \"" },
                { 0x6A, "Numpad *" }, { 0x6B, "Numpad +" }, { 0x6D, "Numpad -" },
                { 0x6E, "Numpad ." }, { 0x6F, "Numpad /" },
            };

            for (var c = 'A'; c <= 'Z'; c++) names[c] = c.ToString();
            for (var c = '0'; c <= '9'; c++) names[c] = c.ToString();
            for (var i = 0; i <= 9; i++) names[0x60 + i] = $"Numpad {i}";
            for (var i = 1; i <= 24; i++) names[0x6F + i] = $"F{i}";

            return names;
        }

        public static string GetName(int vkCode)
        {
            return Names.TryGetValue(vkCode, out var name) ? name : $"Key 0x{vkCode:X2}";
        }
    }
}
