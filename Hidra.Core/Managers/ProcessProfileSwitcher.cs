using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Hidra.Core.Models;
using NLog;

namespace Hidra.Core.Managers
{
    /// <summary>
    /// Polls the focused window's process and activates any profile whose
    /// <see cref="Profile.AutoSwitchExecutable"/> matches it - lets a profile switch itself in
    /// automatically when a particular game/app gets focus, instead of requiring the -p CLI flag.
    /// </summary>
    public sealed class ProcessProfileSwitcher : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly Context _context;
        private readonly DispatcherTimer _timer;
        private string? _lastForegroundExecutable;

        public bool Enabled { get; set; } = true;

        public ProcessProfileSwitcher(Context context)
        {
            _context = context;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (sender, args) => Poll();
            _timer.Start();
        }

        private void Poll()
        {
            if (!Enabled) return;

            string? executable;
            try
            {
                executable = GetForegroundExecutableName();
            }
            catch (Exception e)
            {
                Logger.Trace(e, "Failed to read foreground process");
                return;
            }

            if (executable == null || executable == _lastForegroundExecutable) return;
            _lastForegroundExecutable = executable;

            var profile = FindProfileFor(executable, _context.Profiles);
            if (profile == null) return;
            if (_context.SubscriptionsManager.GetActiveProfile()?.Guid == profile.Guid) return;

            Logger.Info($"Auto-switching to profile '{profile.ProfileBreadCrumbs()}' for {executable}");
            profile.ActivateProfile();
        }

        internal static Profile? FindProfileFor(string executable, List<Profile> profiles)
        {
            foreach (var profile in profiles)
            {
                if (string.Equals(profile.AutoSwitchExecutable, executable, StringComparison.OrdinalIgnoreCase))
                {
                    return profile;
                }

                var childMatch = FindProfileFor(executable, profile.ChildProfiles);
                if (childMatch != null) return childMatch;
            }

            return null;
        }

        private static string? GetForegroundExecutableName()
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;

            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0) return null;

            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName + ".exe";
        }

        public void Dispose()
        {
            _timer.Stop();
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    }
}
