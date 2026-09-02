using System;
using System.Runtime.InteropServices;

namespace Core_RawInputHook.Native
{
    /// <summary>
    /// P/Invoke declarations for the Raw Input API. Used only for mouse movement here - unlike
    /// WM_MOUSEMOVE (which reports OS-processed, pointer-acceleration-adjusted screen position),
    /// RAWMOUSE.lLastX/lLastY are true relative deltas straight from the HID report, which is what
    /// sensitivity/DPI-style scaling needs to behave consistently. Pure user-mode API - no driver.
    /// </summary>
    internal static class RawInputNative
    {
        public const int WM_INPUT = 0x00FF;

        public const ushort HID_USAGE_PAGE_GENERIC = 0x01;
        public const ushort HID_USAGE_GENERIC_MOUSE = 0x02;

        public const uint RIDEV_INPUTSINK = 0x00000100;

        public const uint RID_INPUT = 0x10000003;

        public const uint RIM_TYPEMOUSE = 0;

        /// <summary>Set on RAWMOUSE.usFlags when this is an absolute-position report (e.g. RDP,
        /// tablets) rather than a relative-delta report. We only handle relative mice.</summary>
        public const ushort MOUSE_MOVE_ABSOLUTE = 0x01;

        public const ushort RI_MOUSE_LEFT_BUTTON_DOWN = 0x0001;
        public const ushort RI_MOUSE_LEFT_BUTTON_UP = 0x0002;
        public const ushort RI_MOUSE_RIGHT_BUTTON_DOWN = 0x0004;
        public const ushort RI_MOUSE_RIGHT_BUTTON_UP = 0x0008;
        public const ushort RI_MOUSE_MIDDLE_BUTTON_DOWN = 0x0010;
        public const ushort RI_MOUSE_MIDDLE_BUTTON_UP = 0x0020;
        public const ushort RI_MOUSE_BUTTON_4_DOWN = 0x0040;
        public const ushort RI_MOUSE_BUTTON_4_UP = 0x0080;
        public const ushort RI_MOUSE_BUTTON_5_DOWN = 0x0100;
        public const ushort RI_MOUSE_BUTTON_5_UP = 0x0200;
        public const ushort RI_MOUSE_WHEEL = 0x0400;
        public const ushort RI_MOUSE_HWHEEL = 0x0800;

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWMOUSE
        {
            public ushort usFlags;
            public ushort usButtonFlags;
            public ushort usButtonData;
            public uint ulRawButtons;
            public int lLastX;
            public int lLastY;
            public uint ulExtraInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTMOUSE
        {
            public RAWINPUTHEADER header;
            public RAWMOUSE mouse;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);
    }
}
