using System.Diagnostics;

namespace InputAutomationEngine;

/// <summary>Fired when the foreground application's executable name changes.</summary>
public sealed class ActiveProcessChangedEventArgs : EventArgs
{
    public ActiveProcessChangedEventArgs(string exeName) => ExeName = exeName;

    /// <summary>Executable file name including extension, e.g. "chrome.exe".</summary>
    public string ExeName { get; }
}

/// <summary>
/// Polls the foreground window on a dedicated background thread and raises an event
/// whenever the owning process's executable name changes, so the rest of the engine can
/// switch which <see cref="Profile"/> is active.
/// </summary>
public sealed class ProcessMonitor : IDisposable
{
    private const int PollIntervalMs = 500;
    private const string UnknownExeName = "*";

    private Thread? _thread;
    private volatile bool _running;
    private string _lastExeName = UnknownExeName;

    public event EventHandler<ActiveProcessChangedEventArgs>? ActiveProcessChanged;

    public void Start()
    {
        if (_thread is not null) throw new InvalidOperationException("ProcessMonitor already started.");

        _running = true;
        _thread = new Thread(PollLoop)
        {
            IsBackground = true,
            Name = "InputAutomationEngine.ProcessMonitor",
            Priority = ThreadPriority.BelowNormal,
        };
        _thread.Start();
    }

    private void PollLoop()
    {
        while (_running)
        {
            var exeName = TryResolveForegroundExeName();

            if (!string.Equals(exeName, _lastExeName, StringComparison.OrdinalIgnoreCase))
            {
                _lastExeName = exeName;
                try
                {
                    ActiveProcessChanged?.Invoke(this, new ActiveProcessChangedEventArgs(exeName));
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[ProcessMonitor] Subscriber threw: {ex.Message}");
                }
            }

            Thread.Sleep(PollIntervalMs);
        }
    }

    private static string TryResolveForegroundExeName()
    {
        try
        {
            var hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return UnknownExeName;

            NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0) return UnknownExeName;

            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName + ".exe";
        }
        catch (ArgumentException)
        {
            // The process exited between GetWindowThreadProcessId and GetProcessById.
            return UnknownExeName;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Foreground window belongs to a process at a higher integrity level than us
            // (e.g. an elevated app, or a protected system process) - not something we can
            // resolve without elevation, so fall back to the global profile.
            return UnknownExeName;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ProcessMonitor] Failed to resolve foreground process: {ex.Message}");
            return UnknownExeName;
        }
    }

    public void Dispose()
    {
        _running = false;
        _thread?.Join(TimeSpan.FromSeconds(2));
    }
}
