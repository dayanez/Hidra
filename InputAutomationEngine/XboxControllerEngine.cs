namespace InputAutomationEngine;

/// <summary>Fired once per physical button press (rising edge), never repeated while held.</summary>
public sealed class ControllerButtonEventArgs : EventArgs
{
    public ControllerButtonEventArgs(int controllerIndex, ControllerButton button)
    {
        ControllerIndex = controllerIndex;
        Button = button;
    }

    public int ControllerIndex { get; }
    public ControllerButton Button { get; }
}

/// <summary>
/// Polls up to 4 XInput controllers (Xbox One/Series pads) on a dedicated low-priority
/// background thread and raises an edge-triggered event exactly once per button press,
/// regardless of how long the button is held.
/// </summary>
public sealed class XboxControllerEngine : IDisposable
{
    private const int PollIntervalMs = 16; // ~60 Hz, well within XInput's own polling granularity.

    private static readonly Dictionary<NativeMethods.XInputGamepadButton, ControllerButton> ButtonMap = new()
    {
        [NativeMethods.XInputGamepadButton.A] = ControllerButton.A,
        [NativeMethods.XInputGamepadButton.B] = ControllerButton.B,
        [NativeMethods.XInputGamepadButton.X] = ControllerButton.X,
        [NativeMethods.XInputGamepadButton.Y] = ControllerButton.Y,
        [NativeMethods.XInputGamepadButton.LeftShoulder] = ControllerButton.LeftShoulder,
        [NativeMethods.XInputGamepadButton.RightShoulder] = ControllerButton.RightShoulder,
        [NativeMethods.XInputGamepadButton.Start] = ControllerButton.Start,
        [NativeMethods.XInputGamepadButton.Back] = ControllerButton.Back,
        [NativeMethods.XInputGamepadButton.LeftThumb] = ControllerButton.LeftThumb,
        [NativeMethods.XInputGamepadButton.RightThumb] = ControllerButton.RightThumb,
        [NativeMethods.XInputGamepadButton.DPadUp] = ControllerButton.DPadUp,
        [NativeMethods.XInputGamepadButton.DPadDown] = ControllerButton.DPadDown,
        [NativeMethods.XInputGamepadButton.DPadLeft] = ControllerButton.DPadLeft,
        [NativeMethods.XInputGamepadButton.DPadRight] = ControllerButton.DPadRight,
    };

    private readonly ushort[] _previousButtons = new ushort[NativeMethods.XUSER_MAX_COUNT];
    private readonly bool[] _wasConnected = new bool[NativeMethods.XUSER_MAX_COUNT];

    private Thread? _thread;
    private volatile bool _running;

    /// <summary>Raised once per button press (not release, not while held).</summary>
    public event EventHandler<ControllerButtonEventArgs>? ButtonPressed;

    public void Start()
    {
        if (_thread is not null) throw new InvalidOperationException("XboxControllerEngine already started.");

        _running = true;
        _thread = new Thread(PollLoop)
        {
            IsBackground = true,
            Name = "InputAutomationEngine.XInputPoll",
            Priority = ThreadPriority.BelowNormal,
        };
        _thread.Start();
    }

    private void PollLoop()
    {
        while (_running)
        {
            for (uint i = 0; i < NativeMethods.XUSER_MAX_COUNT; i++)
            {
                PollController(i);
            }

            Thread.Sleep(PollIntervalMs);
        }
    }

    private void PollController(uint index)
    {
        var result = NativeMethods.XInputGetState(index, out var state);
        var connected = result == NativeMethods.ERROR_SUCCESS;

        if (connected != _wasConnected[index])
        {
            Console.WriteLine(connected
                ? $"[XboxControllerEngine] Controller {index} connected."
                : $"[XboxControllerEngine] Controller {index} disconnected.");
            _wasConnected[index] = connected;
            _previousButtons[index] = 0; // Avoid phantom edges from stale state on reconnect.
        }

        if (!connected) return;

        var current = state.Gamepad.wButtons;
        var previous = _previousButtons[index];

        // Rising edge: bits set now that were not set on the last poll.
        var pressedNow = (ushort)(current & ~previous);

        if (pressedNow != 0)
        {
            foreach (var (flag, button) in ButtonMap)
            {
                if (((ushort)flag & pressedNow) != 0)
                {
                    RaiseButtonPressed((int)index, button);
                }
            }
        }

        _previousButtons[index] = current;
    }

    private void RaiseButtonPressed(int controllerIndex, ControllerButton button)
    {
        try
        {
            ButtonPressed?.Invoke(this, new ControllerButtonEventArgs(controllerIndex, button));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[XboxControllerEngine] Subscriber threw handling {button}: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _running = false;
        _thread?.Join(TimeSpan.FromSeconds(2));
    }
}
