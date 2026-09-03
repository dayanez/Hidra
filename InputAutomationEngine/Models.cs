using System.Text.Json.Serialization;

namespace InputAutomationEngine;

/// <summary>
/// The kind of side effect an <see cref="ActionDefinition"/> performs when a
/// keyboard or controller trigger fires.
/// </summary>
public enum ActionType
{
    /// <summary>Launches an executable. <see cref="ActionDefinition.Value"/> is the path/command, <see cref="ActionDefinition.Arguments"/> is optional CLI args.</summary>
    RunProcess,

    /// <summary>Opens a URL in the default browser. <see cref="ActionDefinition.Value"/> is the URL.</summary>
    OpenUrl,

    /// <summary>Synthesizes a keystroke or chord via SendInput. <see cref="ActionDefinition.Value"/> is a "+"-joined chord, or comma-separated chords sent in sequence (e.g. "Ctrl+Shift+Escape").</summary>
    SendKeys,

    /// <summary>A built-in system action. <see cref="ActionDefinition.Value"/> is one of: Lock, VolumeUp, VolumeDown, VolumeMute, MediaPlayPause, MediaNextTrack, MediaPrevTrack.</summary>
    SystemCommand,
}

/// <summary>An action to run when a mapping is triggered.</summary>
public sealed class ActionDefinition
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ActionType Type { get; set; } = ActionType.RunProcess;

    /// <summary>Primary payload: executable path, URL, key chord, or system command name depending on <see cref="Type"/>.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Optional command-line arguments, used only when <see cref="Type"/> is <see cref="ActionType.RunProcess"/>.</summary>
    public string? Arguments { get; set; }
}

/// <summary>Maps a single keyboard key to an action.</summary>
public sealed class KeyMapping
{
    /// <summary>Key name resolved via <see cref="VirtualKeyMap"/> (e.g. "F13", "OemTilde", "D1").</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>When true, the physical keystroke is swallowed and never reaches the focused application.</summary>
    public bool Suppress { get; set; } = true;

    public ActionDefinition Action { get; set; } = new();
}

/// <summary>Xbox controller face/shoulder button names understood by <see cref="XboxControllerEngine"/>.</summary>
public enum ControllerButton
{
    A, B, X, Y,
    LeftShoulder, RightShoulder,
    Start, Back,
    LeftThumb, RightThumb,
    DPadUp, DPadDown, DPadLeft, DPadRight,
}

/// <summary>Maps a single Xbox controller button to an action.</summary>
public sealed class ControllerMapping
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ControllerButton Button { get; set; }

    public ActionDefinition Action { get; set; } = new();
}

/// <summary>
/// One rule set. <see cref="ProcessTarget"/> of "*" is the global/fallback profile;
/// any other value is matched case-insensitively against the foreground process's
/// executable name (e.g. "chrome.exe").
/// </summary>
public sealed class Profile
{
    public string ProcessTarget { get; set; } = "*";

    public List<KeyMapping> KeyMappings { get; set; } = new();

    public List<ControllerMapping> ControllerMappings { get; set; } = new();
}

/// <summary>Root document shape of profiles.json.</summary>
public sealed class ProfileConfig
{
    public List<Profile> Profiles { get; set; } = new();
}
