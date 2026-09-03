using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Hidra.Utilities
{
    /// <summary>
    /// Owns the system tray icon and its context menu. Hidra is meant to run in the background,
    /// so closing the main window hides it here rather than exiting the process; this is what
    /// keeps the app reachable (and exitable) once that happens.
    /// </summary>
    internal sealed class TrayIcon : IDisposable
    {
        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "Hidra";

        private readonly NotifyIcon _notifyIcon;
        private readonly Icon _icon;
        private readonly bool _ownsIcon;

        public event Action ShowRequested;
        public event Action ExitRequested;

        public TrayIcon()
        {
            _icon = LoadAppIcon(out _ownsIcon);

            var showItem = new ToolStripMenuItem("Show Hidra");
            showItem.Click += (_, _) => ShowRequested?.Invoke();

            var startWithWindowsItem = new ToolStripMenuItem("Start with Windows")
            {
                CheckOnClick = true,
                Checked = IsStartWithWindowsEnabled()
            };
            startWithWindowsItem.Click += (_, _) => SetStartWithWindows(startWithWindowsItem.Checked);

            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (_, _) => ExitRequested?.Invoke();

            var menu = new ContextMenuStrip();
            menu.Items.Add(showItem);
            menu.Items.Add(startWithWindowsItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);

            _notifyIcon = new NotifyIcon
            {
                Icon = _icon,
                Text = "Hidra",
                Visible = true,
                ContextMenuStrip = menu
            };
            _notifyIcon.DoubleClick += (_, _) => ShowRequested?.Invoke();
        }

        /// <summary>Shown once, the first time the window is hidden to the tray in a session.</summary>
        public void ShowFirstRunHint()
        {
            _notifyIcon.ShowBalloonTip(3000, "Hidra is still running", "Your mappings stay active in the background. Right-click this icon to reopen or exit.", ToolTipIcon.Info);
        }

        // Extracted from the running exe's own icon resource rather than a loose .ico file, so
        // there's nothing extra to ship or keep in sync with the app icon.
        private static Icon LoadAppIcon(out bool ownsIcon)
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            var icon = exePath != null ? Icon.ExtractAssociatedIcon(exePath) : null;
            ownsIcon = icon != null;
            return icon ?? SystemIcons.Application;
        }

        private static bool IsStartWithWindowsEnabled()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, writable: false);
            return key?.GetValue(RunValueName) != null;
        }

        private static void SetStartWithWindows(bool enabled)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, writable: true);
            if (key == null) return;

            if (enabled)
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(RunValueName, $"\"{exePath}\"");
                }
            }
            else
            {
                key.DeleteValue(RunValueName, throwOnMissingValue: false);
            }
        }

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            if (_ownsIcon) _icon.Dispose();
        }
    }
}
