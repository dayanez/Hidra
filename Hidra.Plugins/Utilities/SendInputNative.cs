using System;
using System.Runtime.InteropServices;

namespace Hidra.Plugins.Utilities
{
    /// <summary>
    /// P/Invoke declarations for synthesizing keyboard input (SendInput) and locking the
    /// workstation. Pure user-mode Win32 API - no driver required.
    /// </summary>
    internal static class SendInputNative
    {
        public const uint INPUT_KEYBOARD = 1;
        public const uint KEYEVENTF_KEYUP = 0x0002;

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            private InputUnion U;

            public static INPUT ForKeyboard(KEYBDINPUT ki)
            {
                return new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = ki } };
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool LockWorkStation();
    }
}
