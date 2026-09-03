namespace InputAutomationEngine;

/// <summary>Arguments delivered for every physical key transition seen by <see cref="KeyboardHook"/>.</summary>
public sealed class KeyHookEventArgs : EventArgs
{
    public KeyHookEventArgs(int vkCode, bool isKeyDown)
    {
        VkCode = vkCode;
        IsKeyDown = isKeyDown;
    }

    public int VkCode { get; }
    public bool IsKeyDown { get; }

    /// <summary>
    /// Set to true by a subscriber to swallow this keystroke so it never reaches the
    /// focused application or any other hook further down the chain.
    /// </summary>
    public bool Suppress { get; set; }
}

/// <summary>
/// Wraps a WH_KEYBOARD_LL global keyboard hook. Windows requires the thread that calls
/// SetWindowsHookEx to keep pumping a message loop for the callback to ever fire, so this
/// class owns a dedicated background thread that installs the hook and runs GetMessage
/// until told to stop.
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    // Kept as a field (not a local) so the GC never collects the delegate while the
    // unmanaged hook still holds a pointer to it - a classic P/Invoke hook pitfall.
    private readonly NativeMethods.LowLevelKeyboardProc _proc;

    private Thread? _thread;
    private uint _threadId;
    private IntPtr _hookId = IntPtr.Zero;
    private readonly ManualResetEventSlim _started = new(false);
    private volatile bool _disposed;

    public KeyboardHook()
    {
        _proc = HookCallback;
    }

    /// <summary>Raised for every key-down and key-up event seen system-wide.</summary>
    public event EventHandler<KeyHookEventArgs>? KeyEvent;

    /// <summary>Installs the hook on a dedicated STA thread and blocks until it is active.</summary>
    public void Start()
    {
        if (_thread is not null) throw new InvalidOperationException("KeyboardHook already started.");

        _thread = new Thread(RunMessageLoop)
        {
            IsBackground = true,
            Name = "InputAutomationEngine.KeyboardHook",
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();

        if (!_started.Wait(TimeSpan.FromSeconds(5)))
        {
            throw new InvalidOperationException("Timed out waiting for the keyboard hook thread to initialize.");
        }

        if (_hookId == IntPtr.Zero)
        {
            throw new InvalidOperationException("SetWindowsHookEx failed to install the low-level keyboard hook.");
        }
    }

    private void RunMessageLoop()
    {
        _threadId = NativeMethods.GetCurrentThreadId();

        using var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
        using var currentModule = currentProcess.MainModule!;
        var moduleHandle = NativeMethods.GetModuleHandle(currentModule.ModuleName);

        _hookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _proc, moduleHandle, 0);
        _started.Set();

        if (_hookId == IntPtr.Zero)
        {
            return;
        }

        // A Win32 message loop is mandatory here: WH_KEYBOARD_LL callbacks are delivered
        // via a hidden window message dispatched to this thread's message queue.
        while (NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg);
        }

        NativeMethods.UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var msg = (int)wParam;
            var isDown = msg is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN;
            var isUp = msg is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP;

            if (isDown || isUp)
            {
                var data = System.Runtime.InteropServices.Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                var args = new KeyHookEventArgs((int)data.vkCode, isDown);

                try
                {
                    KeyEvent?.Invoke(this, args);
                }
                catch (Exception ex)
                {
                    // The hook callback runs on Windows' time budget (default ~5s system-wide
                    // LowLevelHooksTimeout before Windows silently unhooks us); a subscriber
                    // exception must never propagate out of this callback.
                    Console.Error.WriteLine($"[KeyboardHook] Subscriber threw: {ex.Message}");
                }

                if (args.Suppress)
                {
                    return (IntPtr)1;
                }
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    /// <summary>Unhooks and stops the message-pump thread. Safe to call multiple times.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_threadId != 0)
        {
            NativeMethods.PostThreadMessage(_threadId, NativeMethods.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        }

        _thread?.Join(TimeSpan.FromSeconds(2));
        _started.Dispose();
    }
}
