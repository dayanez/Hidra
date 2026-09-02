using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
using System.Threading;
using Core_RawInputHook.Native;
using Hidwizards.IOWrapper.Libraries.SubscriptionHandlers;
using HidWizards.IOWrapper.DataTransferObjects;
using HidWizards.IOWrapper.ProviderInterface.Interfaces;

namespace Core_RawInputHook
{
    /// <summary>
    /// Driver-free keyboard capture/remap provider. Uses a global low-level keyboard hook
    /// (WH_KEYBOARD_LL) to capture and optionally block real keypresses, and SendInput to
    /// synthesize output - both pure user-mode Win32 APIs, no kernel driver required.
    ///
    /// v1 scope: keyboard only, as a single aggregate "Keyboard" device (multiple physical
    /// keyboards are not distinguished - see the project notes on why). Mouse support (buttons
    /// and, for accurate relative-delta axis capture, Raw Input) is a separate follow-up.
    /// </summary>
    [Export(typeof(IProvider))]
    public class Core_RawInputHook : IInputProvider, IOutputProvider, IBindModeProvider
    {
        public string ProviderName => nameof(Core_RawInputHook);
        public bool IsLive { get; private set; }

        private readonly DeviceDescriptor _keyboardDescriptor = new DeviceDescriptor { DeviceHandle = "Keyboard", DeviceInstance = 0 };
        private readonly ProviderReport _providerReport;
        private readonly DeviceReport _keyboardDeviceReport;
        private readonly Dictionary<BindingDescriptor, BindingReport> _keyboardBindingReports = new Dictionary<BindingDescriptor, BindingReport>();

        private SubscriptionHandler _subscriptionHandler;
        private uint _hookThreadId;
        private IntPtr _keyboardHookHandle;
        // Keeping this delegate as a field (not a local/lambda) is required - otherwise the GC can
        // collect it while the unmanaged hook chain still holds a function pointer to it.
        private HookNative.HookProc _keyboardHookProc;
        private DetectionMode _detectionMode = DetectionMode.Subscription;
        private Action<ProviderDescriptor, DeviceDescriptor, BindingReport, short> _bindModeCallback;

        public Core_RawInputHook()
        {
            _keyboardDeviceReport = BuildKeyboardDeviceReport();
            _providerReport = new ProviderReport
            {
                Title = "Raw Input Hook",
                Description = "Driver-free keyboard capture/remap via a global low-level hook",
                API = "RawInputHook",
                ProviderDescriptor = new ProviderDescriptor { ProviderName = ProviderName }
            };

            StartHookThread();
        }

        #region Setup

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

        private void StartHookThread()
        {
            _subscriptionHandler = new SubscriptionHandler(_keyboardDescriptor, (sender, args) => { }, CallbackHandler);

            var ready = new ManualResetEventSlim(false);
            var hookThread = new Thread(() => HookThreadProc(ready)) { IsBackground = true, Name = "Hidra RawInputHook" };
            hookThread.Start();
            // Wait for the hook to actually be installed before returning, so IsLive is accurate
            // as soon as the constructor finishes.
            ready.Wait(TimeSpan.FromSeconds(2));
        }

        private void HookThreadProc(ManualResetEventSlim ready)
        {
            _hookThreadId = HookNative.GetCurrentThreadId();
            _keyboardHookProc = KeyboardHookProc;
            _keyboardHookHandle = HookNative.SetWindowsHookEx(HookNative.WH_KEYBOARD_LL, _keyboardHookProc, HookNative.GetModuleHandle(null), 0);
            IsLive = _keyboardHookHandle != IntPtr.Zero;
            ready.Set();

            if (!IsLive) return;

            // WH_KEYBOARD_LL requires the installing thread to keep pumping messages for the
            // lifetime of the hook, or Windows will silently remove it.
            while (HookNative.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                HookNative.TranslateMessage(ref msg);
                HookNative.DispatchMessage(ref msg);
            }

            if (_keyboardHookHandle != IntPtr.Zero)
            {
                HookNative.UnhookWindowsHookEx(_keyboardHookHandle);
                _keyboardHookHandle = IntPtr.Zero;
            }
        }

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

                            if (_detectionMode == DetectionMode.Bind)
                            {
                                // Report the press for the bind-mode UI, but don't consume it -
                                // the key should keep working normally while the user is choosing
                                // what to bind.
                                _bindModeCallback?.Invoke(_providerReport.ProviderDescriptor, _keyboardDescriptor, bindingReport, value);
                            }
                            else
                            {
                                var blockRequested = _subscriptionHandler.FireCallbacks(bindingDescriptor, value);
                                if (blockRequested) return (IntPtr)1;
                            }
                        }
                    }
                }
            }

            return HookNative.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
        }

        private static void CallbackHandler(InputSubscriptionRequest subReq, short value)
        {
            subReq.Callback?.Invoke(value);
        }

        #endregion

        #region IProvider

        public void RefreshLiveState()
        {
            // The hook is installed for the lifetime of the provider - nothing to refresh.
        }

        public void RefreshDevices()
        {
        }

        public void Dispose()
        {
            if (_hookThreadId != 0)
            {
                HookNative.PostThreadMessage(_hookThreadId, HookNative.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            }
        }

        #endregion

        #region IInputProvider

        public ProviderReport GetInputList()
        {
            _providerReport.Devices = new List<DeviceReport> { _keyboardDeviceReport };
            return _providerReport;
        }

        public DeviceReport GetInputDeviceReport(DeviceDescriptor deviceDescriptor)
        {
            return deviceDescriptor.DeviceHandle == _keyboardDescriptor.DeviceHandle ? _keyboardDeviceReport : null;
        }

        public bool SubscribeInput(InputSubscriptionRequest subReq)
        {
            _subscriptionHandler.Subscribe(subReq);
            return true;
        }

        public bool UnsubscribeInput(InputSubscriptionRequest subReq)
        {
            _subscriptionHandler.Unsubscribe(subReq);
            return true;
        }

        #endregion

        #region IOutputProvider

        public ProviderReport GetOutputList()
        {
            _providerReport.Devices = new List<DeviceReport> { _keyboardDeviceReport };
            return _providerReport;
        }

        public DeviceReport GetOutputDeviceReport(DeviceDescriptor deviceDescriptor)
        {
            return GetInputDeviceReport(deviceDescriptor);
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

        #endregion

        #region IBindModeProvider

        public void SetDetectionMode(DetectionMode detectionMode, DeviceDescriptor deviceDescriptor, Action<ProviderDescriptor, DeviceDescriptor, BindingReport, short> callback = null)
        {
            _detectionMode = detectionMode;
            _bindModeCallback = callback;
        }

        #endregion
    }
}
