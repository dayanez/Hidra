namespace InputAutomationEngine;

/// <summary>
/// Bidirectional lookup between human-readable key names used in profiles.json
/// (e.g. "F13", "OemTilde", "Ctrl") and Win32 virtual-key codes.
/// </summary>
internal static class VirtualKeyMap
{
    private static readonly Dictionary<string, int> NameToVk = BuildMap();
    private static readonly Dictionary<int, string> VkToName =
        NameToVk.GroupBy(kv => kv.Value).ToDictionary(g => g.Key, g => g.First().Key);

    /// <summary>Resolves a key name from profiles.json to a virtual-key code. Returns null if unknown.</summary>
    public static int? Resolve(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        var trimmed = name.Trim();

        // Allow raw numeric / hex VK codes ("0x70", "112") for full flexibility.
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(trimmed.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out var hex))
        {
            return hex;
        }
        if (int.TryParse(trimmed, out var num)) return num;

        return NameToVk.TryGetValue(trimmed, out var vk) ? vk : null;
    }

    /// <summary>Best-effort friendly name for a VK code, used for logging.</summary>
    public static string NameOf(int vkCode) => VkToName.TryGetValue(vkCode, out var name) ? name : $"VK_0x{vkCode:X2}";

    private static Dictionary<string, int> BuildMap()
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Letters / digits map 1:1 onto ASCII in the VK table.
        for (var c = 'A'; c <= 'Z'; c++) map[c.ToString()] = c;
        for (var c = '0'; c <= '9'; c++) map["D" + c] = c;

        // Function keys F1-F24.
        for (var i = 1; i <= 24; i++) map[$"F{i}"] = 0x6F + i;

        // Numpad.
        for (var i = 0; i <= 9; i++) map[$"Numpad{i}"] = 0x60 + i;
        map["Multiply"] = 0x6A;
        map["Add"] = 0x6B;
        map["Separator"] = 0x6C;
        map["Subtract"] = 0x6D;
        map["Decimal"] = 0x6E;
        map["Divide"] = 0x6F;

        map["Back"] = 0x08;
        map["Backspace"] = 0x08;
        map["Tab"] = 0x09;
        map["Clear"] = 0x0C;
        map["Enter"] = 0x0D;
        map["Return"] = 0x0D;
        map["Shift"] = 0x10;
        map["Ctrl"] = 0x11;
        map["Control"] = 0x11;
        map["Alt"] = 0x12;
        map["Menu"] = 0x12;
        map["Pause"] = 0x13;
        map["CapsLock"] = 0x14;
        map["Escape"] = 0x1B;
        map["Esc"] = 0x1B;
        map["Space"] = 0x20;
        map["PageUp"] = 0x21;
        map["PageDown"] = 0x22;
        map["End"] = 0x23;
        map["Home"] = 0x24;
        map["Left"] = 0x25;
        map["Up"] = 0x26;
        map["Right"] = 0x27;
        map["Down"] = 0x28;
        map["PrintScreen"] = 0x2C;
        map["Insert"] = 0x2D;
        map["Delete"] = 0x2E;
        map["Del"] = 0x2E;

        map["LWin"] = 0x5B;
        map["RWin"] = 0x5C;
        map["Win"] = 0x5B;
        map["Apps"] = 0x5D;

        map["LShift"] = 0xA0;
        map["RShift"] = 0xA1;
        map["LCtrl"] = 0xA2;
        map["RCtrl"] = 0xA3;
        map["LAlt"] = 0xA4;
        map["RAlt"] = 0xA5;

        map["BrowserBack"] = 0xA6;
        map["BrowserForward"] = 0xA7;
        map["BrowserRefresh"] = 0xA8;

        map["VolumeMute"] = 0xAD;
        map["VolumeDown"] = 0xAE;
        map["VolumeUp"] = 0xAF;
        map["MediaNextTrack"] = 0xB0;
        map["MediaPrevTrack"] = 0xB1;
        map["MediaStop"] = 0xB2;
        map["MediaPlayPause"] = 0xB3;

        map["OemPlus"] = 0xBB;
        map["OemComma"] = 0xBC;
        map["OemMinus"] = 0xBD;
        map["OemPeriod"] = 0xBE;

        // US keyboard OEM punctuation keys.
        map["Oem1"] = 0xBA;      // ;:
        map["Oem2"] = 0xBF;      // /?
        map["Oem3"] = 0xC0;      // `~
        map["OemTilde"] = 0xC0;
        map["Oem4"] = 0xDB;      // [{
        map["Oem5"] = 0xDC;      // \|
        map["Oem6"] = 0xDD;      // ]}
        map["Oem7"] = 0xDE;      // '"
        map["Oem8"] = 0xDF;

        // F13-F24 (already covered by the F1-F24 loop above) are ideal free macro
        // triggers, since they rarely conflict with anything the OS or apps consume.

        return map;
    }
}
