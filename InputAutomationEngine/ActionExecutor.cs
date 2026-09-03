using System.Diagnostics;
using System.Runtime.InteropServices;

namespace InputAutomationEngine;

/// <summary>
/// Executes <see cref="ActionDefinition"/> instances. All entry points swallow and log
/// exceptions rather than propagate, since callers (the keyboard hook callback, the
/// controller poll loop) must never be taken down by a bad profile entry or a failed
/// process launch.
/// </summary>
internal sealed class ActionExecutor
{
    private static readonly int InputSize = Marshal.SizeOf<NativeMethods.INPUT>();

    public void Execute(ActionDefinition? action)
    {
        if (action is null || string.IsNullOrWhiteSpace(action.Value))
        {
            Console.Error.WriteLine("[ActionExecutor] Skipped: action or action value is empty.");
            return;
        }

        try
        {
            switch (action.Type)
            {
                case ActionType.RunProcess:
                    RunProcess(action.Value, action.Arguments);
                    break;
                case ActionType.OpenUrl:
                    OpenUrl(action.Value);
                    break;
                case ActionType.SendKeys:
                    SendKeySequence(action.Value);
                    break;
                case ActionType.SystemCommand:
                    RunSystemCommand(action.Value);
                    break;
                default:
                    Console.Error.WriteLine($"[ActionExecutor] Unknown action type '{action.Type}'.");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ActionExecutor] Action '{action.Type}:{action.Value}' failed: {ex.Message}");
        }
    }

    private static void RunProcess(string path, string? arguments)
    {
        try
        {
            var psi = new ProcessStartInfo(path)
            {
                Arguments = arguments ?? string.Empty,
                UseShellExecute = true,
            };
            Process.Start(psi);
            Console.WriteLine($"[ActionExecutor] Launched process: {path} {arguments}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ActionExecutor] Failed to launch process '{path}': {ex.Message}");
        }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            // UseShellExecute routes through the shell so the user's default browser opens,
            // rather than requiring this process to know how to launch a browser directly.
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            Console.WriteLine($"[ActionExecutor] Opened URL: {url}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ActionExecutor] Failed to open URL '{url}': {ex.Message}");
        }
    }

    /// <summary>
    /// Sends one or more key chords via SendInput. Chords are separated by ',' and are sent
    /// in sequence; within a chord, keys are separated by '+' and are pressed together
    /// (e.g. "Ctrl+Shift+Escape, Enter").
    /// </summary>
    private static void SendKeySequence(string sequence)
    {
        var chords = sequence.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (chords.Length == 0)
        {
            Console.Error.WriteLine("[ActionExecutor] SendKeys action had no chords to send.");
            return;
        }

        foreach (var chord in chords)
        {
            SendChord(chord);
            Thread.Sleep(15);
        }
    }

    private static void SendChord(string chord)
    {
        var tokens = chord.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var vkCodes = new List<int>();

        foreach (var token in tokens)
        {
            var vk = VirtualKeyMap.Resolve(token);
            if (vk is null)
            {
                Console.Error.WriteLine($"[ActionExecutor] Unknown key name '{token}' in chord '{chord}'; skipping chord.");
                return;
            }
            vkCodes.Add(vk.Value);
        }

        if (vkCodes.Count == 0) return;

        // Press in order, release in reverse order, matching how a human would hold a chord.
        var downInputs = vkCodes.Select(vk => BuildKeyInput(vk, keyUp: false)).ToArray();
        var upInputs = ((IEnumerable<int>)vkCodes).Reverse().Select(vk => BuildKeyInput(vk, keyUp: true)).ToArray();

        var sentDown = NativeMethods.SendInput((uint)downInputs.Length, downInputs, InputSize);
        Thread.Sleep(10);
        var sentUp = NativeMethods.SendInput((uint)upInputs.Length, upInputs, InputSize);

        if (sentDown != downInputs.Length || sentUp != upInputs.Length)
        {
            Console.Error.WriteLine($"[ActionExecutor] SendInput reported a short write for chord '{chord}'.");
        }
    }

    private static void RunSystemCommand(string command)
    {
        switch (command.Trim().ToLowerInvariant())
        {
            case "lock":
                NativeMethods.LockWorkStation();
                Console.WriteLine("[ActionExecutor] Locked workstation.");
                break;
            case "volumeup":
                SendSingleKey(0xAF); // VK_VOLUME_UP
                break;
            case "volumedown":
                SendSingleKey(0xAE); // VK_VOLUME_DOWN
                break;
            case "volumemute":
                SendSingleKey(0xAD); // VK_VOLUME_MUTE
                break;
            case "mediaplaypause":
                SendSingleKey(0xB3); // VK_MEDIA_PLAY_PAUSE
                break;
            case "medianexttrack":
                SendSingleKey(0xB0); // VK_MEDIA_NEXT_TRACK
                break;
            case "mediaprevtrack":
                SendSingleKey(0xB1); // VK_MEDIA_PREV_TRACK
                break;
            default:
                Console.Error.WriteLine($"[ActionExecutor] Unknown SystemCommand '{command}'.");
                break;
        }
    }

    private static void SendSingleKey(int vk)
    {
        var inputs = new[] { BuildKeyInput(vk, keyUp: false), BuildKeyInput(vk, keyUp: true) };
        NativeMethods.SendInput((uint)inputs.Length, inputs, InputSize);
    }

    private static NativeMethods.INPUT BuildKeyInput(int vk, bool keyUp) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        U = new NativeMethods.InputUnion
        {
            ki = new NativeMethods.KEYBDINPUT
            {
                wVk = (ushort)vk,
                wScan = 0,
                dwFlags = keyUp ? NativeMethods.KEYEVENTF_KEYUP : 0,
                time = 0,
                dwExtraInfo = IntPtr.Zero,
            },
        },
    };
}
