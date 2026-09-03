using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
using System.Threading;
using Core_RawInputHook.Native;
using Hidra.IOWrapper.Libraries.SubscriptionHandlers;
using Hidra.IOWrapper.DataTransferObjects;
using Hidra.IOWrapper.ProviderInterface.Interfaces;

namespace Core_RawInputHook
{
    /// <summary>
    /// Driver-free keyboard + mouse capture/remap provider. Uses global low-level hooks
    /// (WH_KEYBOARD_LL / WH_MOUSE_LL) to capture and optionally block real input, Raw Input for
    /// true relative mouse movement deltas, and SendInput to synthesize output - all pure
    /// user-mode Win32 APIs, no kernel driver required.
    ///
    /// Reported as two aggregate devices ("Keyboard (any)" and "Mouse (any)") rather than
    /// distinguishing individual physical devices - see the class-level notes in the keyboard-only
    /// commit this built on for why. Mouse movement axes are not blockable (matching Interception's
    /// own behavior): Hidra observes and can remap movement to another output, but never
    /// suppresses your physical mouse's own cursor movement.
    /// </summary>
    [Export(typeof(IProvider))]
    public class Core_RawInputHook : IInputProvider, IOutputProvider, IBindModeProvider
    {
        public string ProviderName => nameof(Core_RawInputHook);
        public bool IsLive { get; private set; }

        private readonly DeviceDescriptor _keyboardDescriptor = new DeviceDescriptor { DeviceHandle = "Keyboard", DeviceInstance = 0 };
        private readonly DeviceDescriptor _mouseDescriptor = new DeviceDescriptor { DeviceHandle = "Mouse", DeviceInstance = 0 };
        private readonly ProviderReport _providerReport;
        private readonly DeviceReport _keyboardDeviceReport;
        private readonly DeviceReport _mouseDeviceReport;
        private readonly Dictionary<BindingDescriptor, BindingReport> _keyboardBindingReports = new Dictionary<BindingDescriptor, BindingReport>();
        private readonly Dictionary<BindingDescriptor, BindingReport> _mouseButtonBindingReports = new Dictionary<BindingDescriptor, BindingReport>();
        private readonly Dictionary<BindingDescriptor, BindingReport> _mouseAxisBindingReports = new Dictionary<BindingDescriptor, BindingReport>();

        private SubscriptionHandler _keyboardSubscriptionHandler;
        private SubscriptionHandler _mouseSubscriptionHandler;

        private Thread _hookThread;
        private uint _hookThreadId;
        private IntPtr _keyboardHookHandle;
        private IntPtr _mouseHookHandle;
        private IntPtr _windowHandle;
        // Keeping these delegates as fields (not locals/lambdas) is required - otherwise the GC
        // can collect them while the unmanaged hook chain/window class still holds function
        // pointers to them.
        private HookNative.HookProc _keyboardHookProc;
        private HookNative.HookProc _mouseHookProc;
        private WindowNative.WndProc _wndProc;

        private DetectionMode _detectionMode = DetectionMode.Subscription;
        private DeviceDescriptor _bindModeDevice;
        private Action<ProviderDescriptor, DeviceDescriptor, BindingReport, short> _bindModeCallback;

        public Core_RawInputHook()
        {
            _keyboardDeviceReport = BuildKeyboardDeviceReport();
            _mouseDeviceReport = BuildMouseDeviceReport();
            _providerReport = new ProviderReport
            {
                Title = "Raw Input Hook",
                Description = "Driver-free keyboard/mouse capture/remap via global low-level hooks and Raw Input",
                API = "RawInputHook",
                ProviderDescriptor = new ProviderDescriptor { ProviderName = ProviderName }
            };

            StartHookThread();
        }

        #region Device reports

        private DeviceReport BuildKeyboardDeviceReport()
        {
            var keysNode = new DeviceReportNode { Title = "Keys" };
            foreach (var vk in VirtualKeyNames.Names.Keys)
            {
                var bindingDescriptor = new BindingDescriptor { Type = BindingType.Button, Index = vk };
                var bindingReport = new BindingReport
                {
                    Title = VirtualKeyNames.GetName(vk),
                    Path = $"Key: {VirtualKeyNames.GetName(vk)}",
                    Category = BindingCategory.Momentary,
                    BindingDescriptor = bindingDescriptor,
                    Blockable = true
                };
                keysNode.Bindings.Add(bindingReport);
                _keyboardBindingReports[bindingDescriptor] = bindingReport;
            }
            keysNode.Bindings.Sort((x, y) => string.Compare(x.Title, y.Title, StringComparison.Ordinal));

            return new DeviceReport
            {
                DeviceName = "Keyboard (any)",
                DeviceDescriptor = _keyboardDescriptor,
                Nodes = new List<DeviceReportNode> { keysNode }
            };
        }

        private DeviceReport BuildMouseDeviceReport()
        {
            var buttonsNode = new DeviceReportNode { Title = "Buttons" };
            for (var i = 0; i < MouseNames.ButtonNames.Count; i++)
            {
                var bindingDescriptor = new BindingDescriptor { Type = BindingType.Button, Index = i };
                var bindingReport = new BindingReport
                {
                    Title = MouseNames.ButtonNames[i],
                    Path = $"Button: {MouseNames.ButtonNames[i]}",
                    Category = BindingCategory.Momentary,
                    BindingDescriptor = bindingDescriptor,
                    Blockable = true
                };
                buttonsNode.Bindings.Add(bindingReport);
                _mouseButtonBindingReports[bindingDescriptor] = bindingReport;
            }

            var axesNode = new DeviceReportNode { Title = "Axes" };
            foreach (var (index, title) in new[] { (MouseNames.AxisX, "X"), (MouseNames.AxisY, "Y") })
            {
                var bindingDescriptor = new BindingDescriptor { Type = BindingType.Axis, Index = index };
                var bindingReport = new BindingReport
                {
                    Title = title,
                    Path = $"Delta Axis: {title}",
                    Category = BindingCategory.Delta,
                    BindingDescriptor = bindingDescriptor,
                    // Matches Interception's own behavior: movement is observable/remappable to
                    // another output, but Hidra never suppresses your physical mouse's own cursor.
                    Blockable = false
                };
                axesNode.Bindings.Add(bindingReport);
                _mouseAxisBindingReports[bindingDescriptor] = bindingReport;
            }

            return new DeviceReport
            {
                DeviceName = "Mouse (any)",
                DeviceDescriptor = _mouseDescriptor,
                Nodes = new List<DeviceReportNode> { buttonsNode, axesNode }
            };
        }

        #endregion

        #region Hook thread / message pump

        private void StartHookThread()
        {
            _keyboardSubscriptionHandler = new SubscriptionHandler(_keyboardDescriptor, (sender, args) => { }, CallbackHandler);
            _mouseSubscriptionHandler = new SubscriptionHandler(_mouseDescriptor, (sender, args) => { }, CallbackHandler);

            var ready = new ManualResetEventSlim(false);
            _hookThread = new Thread(() => HookThreadProc(ready)) { IsBackground = true, Name = "Hidra RawInputHook" };
            _hookThread.Start();
            // Wait for setup to finish before returning, so IsLive is accurate as soon as the
            // constructor returns.
            ready.Wait(TimeSpan.FromSeconds(2));
        }

        private void HookThreadProc(ManualResetEventSlim ready)
        {
            _hookThreadId = HookNative.GetCurrentThreadId();

            _windowHandle = CreateMessageWindow();
            RegisterMouseRawInput(_windowHandle);

            _keyboardHookProc = KeyboardHookProc;
            _keyboardHookHandle = HookNative.SetWindowsHookEx(HookNative.WH_KEYBOARD_LL, _keyboardHookProc, HookNative.GetModuleHandle(null), 0);

            _mouseHookProc = MouseHookProc;
            _mouseHookHandle = HookNative.SetWindowsHookEx(HookNative.WH_MOUSE_LL, _mouseHookProc, HookNative.GetModuleHandle(null), 0);

            IsLive = _keyboardHookHandle != IntPtr.Zero && _mouseHookHandle != IntPtr.Zero && _windowHandle != IntPtr.Zero;
            ready.Set();

            if (!IsLive) return;

            while (HookNative.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                HookNative.TranslateMessage(ref msg);
                HookNative.DispatchMessage(ref msg);
            }

            if (_keyboardHookHandle != IntPtr.Zero) HookNative.UnhookWindowsHookEx(_keyboardHookHandle);
            if (_mouseHookHandle != IntPtr.Zero) HookNative.UnhookWindowsHookEx(_mouseHookHandle);
            if (_windowHandle != IntPtr.Zero) WindowNative.DestroyWindow(_windowHandle);

            IsLive = false;
        }

        private IntPtr CreateMessageWindow()
        {
            _wndProc = WndProc;
            var className = $"HidraRawInputHook_{Guid.NewGuid():N}";

            var wc = new WindowNative.WNDCLASSEX
            {
                cbSize = Marshal.SizeOf<WindowNative.WNDCLASSEX>(),
                lpfnWndProc = _wndProc,
                hInstance = HookNative.GetModuleHandle(null),
                lpszClassName = className
            };

            if (WindowNative.RegisterClassEx(ref wc) == 0) return IntPtr.Zero;

            return WindowNative.CreateWindowEx(0, className, className, 0, 0, 0, 0, 0,
                WindowNative.HWND_MESSAGE, IntPtr.Zero, wc.hInstance, IntPtr.Zero);
        }

        private static void RegisterMouseRawInput(IntPtr targetWindow)
        {
            if (targetWindow == IntPtr.Zero) return;

            var devices = new[]
            {
                new RawInputNative.RAWINPUTDEVICE
                {
                    usUsagePage = RawInputNative.HID_USAGE_PAGE_GENERIC,
                    usUsage = RawInputNative.HID_USAGE_GENERIC_MOUSE,
                    dwFlags = RawInputNative.RIDEV_INPUTSINK,
                    hwndTarget = targetWindow
                }
            };

            RawInputNative.RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RawInputNative.RAWINPUTDEVICE>());
        }

        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == RawInputNative.WM_INPUT)
            {
                HandleRawInputMouse(lParam);
            }

            return WindowNative.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        #endregion

        #region Keyboard capture

        private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode == HookNative.HC_ACTION)
            {
                var data = Marshal.PtrToStructure<HookNative.KBDLLHOOKSTRUCT>(lParam);
                // Windows sets this flag on any key event that came from SendInput, ours or
                // anyone else's - always let those through untouched, or our own output would
                // loop back through this same hook.
                if ((data.flags & HookNative.LLKHF_INJECTED) == 0)
                {
                    var message = (int)wParam;
                    var isDown = message == HookNative.WM_KEYDOWN || message == HookNative.WM_SYSKEYDOWN;
                    var isUp = message == HookNative.WM_KEYUP || message == HookNative.WM_SYSKEYUP;

                    if (isDown || isUp)
                    {
                        var bindingDescriptor = new BindingDescriptor { Type = BindingType.Button, Index = (int)data.vkCode };
                        if (_keyboardBindingReports.TryGetValue(bindingDescriptor, out var bindingReport))
                        {
                            var value = (short)(isDown ? 1 : 0);

                            if (_detectionMode == DetectionMode.Bind && _bindModeDevice.DeviceHandle == _keyboardDescriptor.DeviceHandle)
                            {
                                // Report the press for the bind-mode UI, but don't consume it -
                                // the key should keep working normally while the user is choosing
                                // what to bind.
                                _bindModeCallback?.Invoke(_providerReport.ProviderDescriptor, _keyboardDescriptor, bindingReport, value);
                            }
                            else if (_detectionMode == DetectionMode.Subscription)
                            {
                                var blockRequested = _keyboardSubscriptionHandler.FireCallbacks(bindingDescriptor, value);
                                if (blockRequested) return (IntPtr)1;
                            }
                        }
                    }
                }
            }

            return HookNative.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
        }

        #endregion

        #region Mouse capture

        private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode == HookNative.HC_ACTION)
            {
                var data = Marshal.PtrToStructure<HookNative.MSLLHOOKSTRUCT>(lParam);
                if ((data.flags & HookNative.LLMHF_INJECTED) == 0)
                {
                    var message = (int)wParam;
                    var buttonUpdate = GetButtonUpdate(message, data.mouseData);
                    if (buttonUpdate != null)
                    {
                        var (index, value) = buttonUpdate.Value;
                        var bindingDescriptor = new BindingDescriptor { Type = BindingType.Button, Index = index };
                        if (_mouseButtonBindingReports.TryGetValue(bindingDescriptor, out var bindingReport))
                        {
                            if (_detectionMode == DetectionMode.Bind && _bindModeDevice.DeviceHandle == _mouseDescriptor.DeviceHandle)
                            {
                                _bindModeCallback?.Invoke(_providerReport.ProviderDescriptor, _mouseDescriptor, bindingReport, value);
                            }
                            else if (_detectionMode == DetectionMode.Subscription)
                            {
                                var blockRequested = _mouseSubscriptionHandler.FireCallbacks(bindingDescriptor, value);
                                // Wheel events are single ticks (no natural "up" state) - fire the
                                // pressed/released pair immediately so plugins see a momentary pulse.
                                if (message == HookNative.WM_MOUSEWHEEL || message == HookNative.WM_MOUSEHWHEEL)
                                {
                                    _mouseSubscriptionHandler.FireCallbacks(bindingDescriptor, 0);
                                }
                                if (blockRequested) return (IntPtr)1;
                            }
                        }
                    }
                }
            }

            return HookNative.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
        }

        /// <summary>Maps a WM_* mouse message to (button index, pressed/released or wheel value), or
        /// null if the message isn't a button/wheel event (e.g. WM_MOUSEMOVE, which we never block).</summary>
        private static (int index, short value)? GetButtonUpdate(int message, uint mouseData)
        {
            switch (message)
            {
                case HookNative.WM_LBUTTONDOWN: return (MouseNames.LeftButton, 1);
                case HookNative.WM_LBUTTONUP: return (MouseNames.LeftButton, 0);
                case HookNative.WM_RBUTTONDOWN: return (MouseNames.RightButton, 1);
                case HookNative.WM_RBUTTONUP: return (MouseNames.RightButton, 0);
                case HookNative.WM_MBUTTONDOWN: return (MouseNames.MiddleButton, 1);
                case HookNative.WM_MBUTTONUP: return (MouseNames.MiddleButton, 0);
                case HookNative.WM_XBUTTONDOWN:
                    return (((mouseData >> 16) == 1 ? MouseNames.XButton1 : MouseNames.XButton2), 1);
                case HookNative.WM_XBUTTONUP:
                    return (((mouseData >> 16) == 1 ? MouseNames.XButton1 : MouseNames.XButton2), 0);
                case HookNative.WM_MOUSEWHEEL:
                    return (unchecked((short)(mouseData >> 16)) > 0 ? MouseNames.WheelUp : MouseNames.WheelDown, 1);
                case HookNative.WM_MOUSEHWHEEL:
                    return (unchecked((short)(mouseData >> 16)) > 0 ? MouseNames.WheelRight : MouseNames.WheelLeft, 1);
                default:
                    return null;
            }
        }

        private void HandleRawInputMouse(IntPtr hRawInput)
        {
            uint size = 0;
            RawInputNative.GetRawInputData(hRawInput, RawInputNative.RID_INPUT, IntPtr.Zero, ref size, (uint)Marshal.SizeOf<RawInputNative.RAWINPUTHEADER>());
            if (size == 0) return;

            var buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                if (RawInputNative.GetRawInputData(hRawInput, RawInputNative.RID_INPUT, buffer, ref size, (uint)Marshal.SizeOf<RawInputNative.RAWINPUTHEADER>()) != size)
                {
                    return;
                }

                var raw = Marshal.PtrToStructure<RawInputNative.RAWINPUTMOUSE>(buffer);
                if (raw.header.dwType != RawInputNative.RIM_TYPEMOUSE) return;
                // Absolute-position reports (RDP sessions, some tablets) aren't true relative
                // deltas - skip rather than feed misleading values to sensitivity-scaling plugins.
                if ((raw.mouse.usFlags & RawInputNative.MOUSE_MOVE_ABSOLUTE) != 0) return;

                if (_detectionMode == DetectionMode.Subscription)
                {
                    if (raw.mouse.lLastX != 0)
                    {
                        var binding = new BindingDescriptor { Type = BindingType.Axis, Index = MouseNames.AxisX };
                        _mouseSubscriptionHandler.FireCallbacks(binding, ClampToShort(raw.mouse.lLastX));
                    }
                    if (raw.mouse.lLastY != 0)
                    {
                        var binding = new BindingDescriptor { Type = BindingType.Axis, Index = MouseNames.AxisY };
                        _mouseSubscriptionHandler.FireCallbacks(binding, ClampToShort(raw.mouse.lLastY));
                    }
                }
                else if (_detectionMode == DetectionMode.Bind && _bindModeDevice.DeviceHandle == _mouseDescriptor.DeviceHandle)
                {
                    if (raw.mouse.lLastX != 0 && _mouseAxisBindingReports.TryGetValue(new BindingDescriptor { Type = BindingType.Axis, Index = MouseNames.AxisX }, out var xReport))
                    {
                        _bindModeCallback?.Invoke(_providerReport.ProviderDescriptor, _mouseDescriptor, xReport, ClampToShort(raw.mouse.lLastX));
                    }
                    if (raw.mouse.lLastY != 0 && _mouseAxisBindingReports.TryGetValue(new BindingDescriptor { Type = BindingType.Axis, Index = MouseNames.AxisY }, out var yReport))
                    {
                        _bindModeCallback?.Invoke(_providerReport.ProviderDescriptor, _mouseDescriptor, yReport, ClampToShort(raw.mouse.lLastY));
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static short ClampToShort(int value)
        {
            if (value > short.MaxValue) return short.MaxValue;
            if (value < short.MinValue) return short.MinValue;
            return (short)value;
        }

        #endregion

        private static void CallbackHandler(InputSubscriptionRequest subReq, short value)
        {
            subReq.Callback?.Invoke(value);
        }

        #region IProvider

        public void RefreshLiveState()
        {
            // The hooks are installed for the lifetime of the provider - nothing to refresh.
        }

        public void RefreshDevices()
        {
        }

        public void Dispose()
        {
            if (_hookThreadId != 0)
            {
                HookNative.PostThreadMessage(_hookThreadId, HookNative.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
                // Wait for the hook thread's message loop to actually exit and unhook before
                // returning, so IsLive is accurate as soon as Dispose() returns.
                _hookThread?.Join(TimeSpan.FromSeconds(2));
            }
        }

        #endregion

        #region IInputProvider

        public ProviderReport GetInputList()
        {
            _providerReport.Devices = new List<DeviceReport> { _keyboardDeviceReport, _mouseDeviceReport };
            return _providerReport;
        }

        public DeviceReport GetInputDeviceReport(DeviceDescriptor deviceDescriptor)
        {
            return GetDeviceReport(deviceDescriptor);
        }

        public bool SubscribeInput(InputSubscriptionRequest subReq)
        {
            GetSubscriptionHandler(subReq.DeviceDescriptor)?.Subscribe(subReq);
            return true;
        }

        public bool UnsubscribeInput(InputSubscriptionRequest subReq)
        {
            GetSubscriptionHandler(subReq.DeviceDescriptor)?.Unsubscribe(subReq);
            return true;
        }

        #endregion

        #region IOutputProvider

        public ProviderReport GetOutputList()
        {
            _providerReport.Devices = new List<DeviceReport> { _keyboardDeviceReport, _mouseDeviceReport };
            return _providerReport;
        }

        public DeviceReport GetOutputDeviceReport(DeviceDescriptor deviceDescriptor)
        {
            return GetDeviceReport(deviceDescriptor);
        }

        public bool SubscribeOutputDevice(OutputSubscriptionRequest subReq)
        {
            return true;
        }

        public bool UnSubscribeOutputDevice(OutputSubscriptionRequest subReq)
        {
            return true;
        }

        public bool SetOutputState(OutputSubscriptionRequest subReq, BindingDescriptor bindingDescriptor, int state)
        {
            if (subReq.DeviceDescriptor.DeviceHandle == _keyboardDescriptor.DeviceHandle)
            {
                return SetKeyboardOutputState(bindingDescriptor, state);
            }
            if (subReq.DeviceDescriptor.DeviceHandle == _mouseDescriptor.DeviceHandle)
            {
                return SetMouseOutputState(bindingDescriptor, state);
            }
            return false;
        }

        private static bool SetKeyboardOutputState(BindingDescriptor bindingDescriptor, int state)
        {
            if (bindingDescriptor.Type != BindingType.Button) return false;

            var input = SendInputNative.INPUT.ForKeyboard(new SendInputNative.KEYBDINPUT
            {
                wVk = (ushort)bindingDescriptor.Index,
                wScan = 0,
                dwFlags = state != 0 ? 0 : SendInputNative.KEYEVENTF_KEYUP,
                time = 0,
                dwExtraInfo = IntPtr.Zero
            });

            return SendInputNative.SendInput(1, new[] { input }, Marshal.SizeOf<SendInputNative.INPUT>()) == 1;
        }

        private static bool SetMouseOutputState(BindingDescriptor bindingDescriptor, int state)
        {
            SendInputNative.MOUSEINPUT mi;

            if (bindingDescriptor.Type == BindingType.Axis)
            {
                mi = new SendInputNative.MOUSEINPUT
                {
                    dx = bindingDescriptor.Index == MouseNames.AxisX ? state : 0,
                    dy = bindingDescriptor.Index == MouseNames.AxisY ? state : 0,
                    dwFlags = SendInputNative.MOUSEEVENTF_MOVE
                };
            }
            else
            {
                var down = state != 0;
                switch (bindingDescriptor.Index)
                {
                    case MouseNames.LeftButton:
                        mi = MakeButtonInput(down ? SendInputNative.MOUSEEVENTF_LEFTDOWN : SendInputNative.MOUSEEVENTF_LEFTUP);
                        break;
                    case MouseNames.RightButton:
                        mi = MakeButtonInput(down ? SendInputNative.MOUSEEVENTF_RIGHTDOWN : SendInputNative.MOUSEEVENTF_RIGHTUP);
                        break;
                    case MouseNames.MiddleButton:
                        mi = MakeButtonInput(down ? SendInputNative.MOUSEEVENTF_MIDDLEDOWN : SendInputNative.MOUSEEVENTF_MIDDLEUP);
                        break;
                    case MouseNames.XButton1:
                        mi = MakeButtonInput(down ? SendInputNative.MOUSEEVENTF_XDOWN : SendInputNative.MOUSEEVENTF_XUP, SendInputNative.XBUTTON1);
                        break;
                    case MouseNames.XButton2:
                        mi = MakeButtonInput(down ? SendInputNative.MOUSEEVENTF_XDOWN : SendInputNative.MOUSEEVENTF_XUP, SendInputNative.XBUTTON2);
                        break;
                    case MouseNames.WheelUp:
                        mi = MakeButtonInput(SendInputNative.MOUSEEVENTF_WHEEL, 120);
                        break;
                    case MouseNames.WheelDown:
                        mi = MakeButtonInput(SendInputNative.MOUSEEVENTF_WHEEL, unchecked((uint)-120));
                        break;
                    case MouseNames.WheelRight:
                        mi = MakeButtonInput(SendInputNative.MOUSEEVENTF_HWHEEL, 120);
                        break;
                    case MouseNames.WheelLeft:
                        mi = MakeButtonInput(SendInputNative.MOUSEEVENTF_HWHEEL, unchecked((uint)-120));
                        break;
                    default:
                        return false;
                }

                // Wheel/tick-style outputs only fire on the "pressed" edge - ignore the paired
                // release we synthesize on the input side.
                if ((bindingDescriptor.Index == MouseNames.WheelUp || bindingDescriptor.Index == MouseNames.WheelDown ||
                     bindingDescriptor.Index == MouseNames.WheelRight || bindingDescriptor.Index == MouseNames.WheelLeft) && !down)
                {
                    return true;
                }
            }

            var input = SendInputNative.INPUT.ForMouse(mi);
            return SendInputNative.SendInput(1, new[] { input }, Marshal.SizeOf<SendInputNative.INPUT>()) == 1;
        }

        private static SendInputNative.MOUSEINPUT MakeButtonInput(uint flags, uint mouseData = 0)
        {
            return new SendInputNative.MOUSEINPUT { dwFlags = flags, mouseData = mouseData };
        }

        #endregion

        #region Shared helpers

        private DeviceReport GetDeviceReport(DeviceDescriptor deviceDescriptor)
        {
            if (deviceDescriptor.DeviceHandle == _keyboardDescriptor.DeviceHandle) return _keyboardDeviceReport;
            if (deviceDescriptor.DeviceHandle == _mouseDescriptor.DeviceHandle) return _mouseDeviceReport;
            return null;
        }

        private SubscriptionHandler GetSubscriptionHandler(DeviceDescriptor deviceDescriptor)
        {
            if (deviceDescriptor.DeviceHandle == _keyboardDescriptor.DeviceHandle) return _keyboardSubscriptionHandler;
            if (deviceDescriptor.DeviceHandle == _mouseDescriptor.DeviceHandle) return _mouseSubscriptionHandler;
            return null;
        }

        #endregion

        #region IBindModeProvider

        public void SetDetectionMode(DetectionMode detectionMode, DeviceDescriptor deviceDescriptor, Action<ProviderDescriptor, DeviceDescriptor, BindingReport, short> callback = null)
        {
            _detectionMode = detectionMode;
            _bindModeDevice = deviceDescriptor;
            _bindModeCallback = callback;
        }

        #endregion
    }
}
