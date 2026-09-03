using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Hidra.Core.Utilities;

namespace Hidra.Plugins.Utilities
{
    public enum ActionType
    {
        RunProcess,
        OpenUrl,
        SendKeys,
        SystemCommand
    }

    /// <summary>
    /// Runs the side effect for a <see cref="Remapper.ButtonToAction"/> trigger. Every entry
    /// point swallows and logs exceptions rather than propagating, since the caller is a
    /// plugin's <c>Update()</c>, which runs on the input provider's capture thread and must
    /// never be taken down by a bad action value or a failed process launch.
    /// </summary>
    internal static class ActionExecutor
    {
        private static readonly int InputSize = Marshal.SizeOf<SendInputNative.INPUT>();

        /// <summary>
        /// Queues the action onto the thread pool so the caller (a plugin's Update, running on
        /// the input provider's capture thread) returns immediately. RunProcess/OpenUrl can
        /// block on shell/COM activation for tens of milliseconds, which would otherwise add
        /// that latency to every keystroke system-wide.
        /// </summary>
        public static void ExecuteAsync(ActionType type, string value, string arguments)
        {
            ThreadPool.QueueUserWorkItem(_ => Execute(type, value, arguments));
        }

        private static void Execute(ActionType type, string value, string arguments)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Logger.Warn("ButtonToAction skipped: action value is empty.");
                return;
            }

            try
            {
                switch (type)
                {
                    case ActionType.RunProcess:
                        RunProcess(value, arguments);
                        break;
                    case ActionType.OpenUrl:
                        OpenUrl(value);
                        break;
                    case ActionType.SendKeys:
                        SendKeySequence(value);
                        break;
                    case ActionType.SystemCommand:
                        RunSystemCommand(value);
                        break;
                    default:
                        Logger.Warn($"ButtonToAction: unknown action type '{type}'.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"ButtonToAction: action '{type}:{value}' failed", ex);
            }
        }

        private static void RunProcess(string path, string arguments)
        {
            var psi = new ProcessStartInfo(path)
            {
                Arguments = arguments ?? string.Empty,
                UseShellExecute = true,
            };
            Process.Start(psi);
        }

        private static void OpenUrl(string url)
        {
            // UseShellExecute routes through the shell so the user's default browser opens,
            // rather than requiring this process to know how to launch a browser directly.
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        /// <summary>
        /// Sends one or more key chords via SendInput. Chords are separated by ',' and are sent
        /// in sequence; within a chord, keys are separated by '+' and are pressed together
        /// (e.g. "Ctrl+Shift+Escape, Enter").
        /// </summary>
        private static void SendKeySequence(string sequence)
        {
            var chords = sequence.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim())
                .Where(c => c.Length > 0)
                .ToArray();

            if (chords.Length == 0)
            {
                Logger.Warn("ButtonToAction: SendKeys action had no chords to send.");
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
            var tokens = chord.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .ToArray();

            var vkCodes = new List<int>();
            foreach (var token in tokens)
            {
                var vk = VirtualKeyMap.Resolve(token);
                if (vk == null)
                {
                    Logger.Warn($"ButtonToAction: unknown key name '{token}' in chord '{chord}'; skipping chord.");
                    return;
                }
                vkCodes.Add(vk.Value);
            }

            if (vkCodes.Count == 0) return;

            // Press in order, release in reverse order, matching how a human would hold a chord.
            var downInputs = vkCodes.Select(vk => BuildKeyInput(vk, keyUp: false)).ToArray();
            var upInputs = Enumerable.Reverse(vkCodes).Select(vk => BuildKeyInput(vk, keyUp: true)).ToArray();

            SendInputNative.SendInput((uint)downInputs.Length, downInputs, InputSize);
            Thread.Sleep(10);
            SendInputNative.SendInput((uint)upInputs.Length, upInputs, InputSize);
        }

        private static void RunSystemCommand(string command)
        {
            switch (command.Trim().ToLowerInvariant())
            {
                case "lock":
                    SendInputNative.LockWorkStation();
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
                    Logger.Warn($"ButtonToAction: unknown SystemCommand '{command}'.");
                    break;
            }
        }

        private static void SendSingleKey(int vk)
        {
            var inputs = new[] { BuildKeyInput(vk, keyUp: false), BuildKeyInput(vk, keyUp: true) };
            SendInputNative.SendInput((uint)inputs.Length, inputs, InputSize);
        }

        private static SendInputNative.INPUT BuildKeyInput(int vk, bool keyUp)
        {
            return SendInputNative.INPUT.ForKeyboard(new SendInputNative.KEYBDINPUT
            {
                wVk = (ushort)vk,
                wScan = 0,
                dwFlags = keyUp ? SendInputNative.KEYEVENTF_KEYUP : 0,
                time = 0,
                dwExtraInfo = IntPtr.Zero,
            });
        }
    }
}
