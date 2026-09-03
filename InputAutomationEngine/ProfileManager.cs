using System.Text.Json;

namespace InputAutomationEngine;

/// <summary>
/// Loads, and if absent generates, profiles.json, and resolves which <see cref="Profile"/>
/// is active for a given foreground process. Thread-safe: <see cref="XboxControllerEngine"/>,
/// <see cref="KeyboardHook"/> callbacks, and <see cref="ProcessMonitor"/> all read the active
/// profile concurrently from different threads.
/// </summary>
internal sealed class ProfileManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _path;
    private readonly object _lock = new();
    private ProfileConfig _config = new();

    public ProfileManager(string path)
    {
        _path = path;
        Load();
    }

    /// <summary>(Re)loads profiles.json from disk, generating a default file first if none exists.</summary>
    public void Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                Console.WriteLine($"[ProfileManager] '{_path}' not found. Generating a default profile file.");
                WriteDefault();
            }

            var json = File.ReadAllText(_path);
            var config = JsonSerializer.Deserialize<ProfileConfig>(json, JsonOptions);

            if (config is null || config.Profiles.Count == 0)
            {
                throw new InvalidDataException("profiles.json deserialized to an empty configuration.");
            }

            lock (_lock)
            {
                _config = config;
            }

            Console.WriteLine($"[ProfileManager] Loaded {config.Profiles.Count} profile(s) from '{_path}'.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ProfileManager] Failed to load '{_path}': {ex.Message}. Falling back to in-memory defaults.");
            lock (_lock)
            {
                _config = BuildDefaultConfig();
            }
        }
    }

    /// <summary>
    /// Returns the profile whose ProcessTarget matches <paramref name="exeName"/> exactly
    /// (case-insensitive), or the "*" global profile if no specific match exists. Specific
    /// profiles fully replace the global one rather than merging with it.
    /// </summary>
    public Profile GetActiveProfile(string exeName)
    {
        lock (_lock)
        {
            var specific = _config.Profiles.FirstOrDefault(p =>
                !string.IsNullOrWhiteSpace(p.ProcessTarget) &&
                p.ProcessTarget != "*" &&
                string.Equals(p.ProcessTarget, exeName, StringComparison.OrdinalIgnoreCase));

            if (specific is not null) return specific;

            return _config.Profiles.FirstOrDefault(p => p.ProcessTarget == "*") ?? new Profile();
        }
    }

    private void WriteDefault()
    {
        try
        {
            var json = JsonSerializer.Serialize(BuildDefaultConfig(), JsonOptions);
            File.WriteAllText(_path, json);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ProfileManager] Could not write default profiles.json: {ex.Message}");
        }
    }

    /// <summary>
    /// Default configuration: a global profile wiring the four required Xbox face-button
    /// actions plus one illustrative keyboard remap, and a process-specific example profile
    /// showing how a title can override the global rules.
    /// </summary>
    private static ProfileConfig BuildDefaultConfig() => new()
    {
        Profiles = new List<Profile>
        {
            new()
            {
                ProcessTarget = "*",
                KeyMappings = new List<KeyMapping>
                {
                    new()
                    {
                        Key = "F13",
                        Suppress = true,
                        Action = new ActionDefinition
                        {
                            Type = ActionType.RunProcess,
                            Value = "notepad.exe",
                        },
                    },
                },
                ControllerMappings = new List<ControllerMapping>
                {
                    new() { Button = ControllerButton.A, Action = new ActionDefinition { Type = ActionType.RunProcess, Value = "cmd.exe" } },
                    new() { Button = ControllerButton.B, Action = new ActionDefinition { Type = ActionType.RunProcess, Value = "wt.exe" } },
                    new() { Button = ControllerButton.X, Action = new ActionDefinition { Type = ActionType.OpenUrl, Value = "https://stackoverflow.com" } },
                    new() { Button = ControllerButton.Y, Action = new ActionDefinition { Type = ActionType.OpenUrl, Value = "https://github.com" } },
                },
            },
            new()
            {
                ProcessTarget = "chrome.exe",
                KeyMappings = new List<KeyMapping>
                {
                    new()
                    {
                        Key = "F13",
                        Suppress = true,
                        Action = new ActionDefinition
                        {
                            Type = ActionType.SendKeys,
                            Value = "Ctrl+T",
                        },
                    },
                },
                ControllerMappings = new List<ControllerMapping>
                {
                    new() { Button = ControllerButton.A, Action = new ActionDefinition { Type = ActionType.SendKeys, Value = "Ctrl+T" } },
                    new() { Button = ControllerButton.B, Action = new ActionDefinition { Type = ActionType.SendKeys, Value = "Ctrl+W" } },
                },
            },
        },
    };
}
