using System;
using System.Runtime.InteropServices;

namespace ClipTyper
{
    static class Native
    {
        public const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8, MOD_NOREPEAT = 0x4000;

        public const ushort VK_TAB = 0x09, VK_RETURN = 0x0D, VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12,
            VK_ESCAPE = 0x1B, VK_END = 0x23, VK_LEFT = 0x25, VK_DOWN = 0x28, VK_LWIN = 0x5B, VK_RWIN = 0x5C;

        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr hIcon);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern short VkKeyScan(char ch);
        [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint mapType);
        [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] inputs, int size);

        public static bool IsDown(int vk) { return (GetAsyncKeyState(vk) & 0x8000) != 0; }

        const uint INPUT_KEYBOARD = 1;
        const uint KEYEVENTF_EXTENDEDKEY = 0x1, KEYEVENTF_KEYUP = 0x2, KEYEVENTF_UNICODE = 0x4;

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
        [StructLayout(LayoutKind.Sequential)]
        struct INPUT { public uint type; public InputUnion u; }

        static INPUT Key(ushort vk, ushort scan, uint flags)
        {
            var i = new INPUT();
            i.type = INPUT_KEYBOARD;
            i.u.ki = new KEYBDINPUT { wVk = vk, wScan = scan, dwFlags = flags };
            return i;
        }

        static void Send(params INPUT[] inputs)
        {
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        // extended = true for arrow keys / Home / End so they aren't read as numpad keys.
        public static void TapVk(ushort vk, bool extended = false)
        {
            ushort sc = (ushort)MapVirtualKey(vk, 0);
            uint ext = extended ? KEYEVENTF_EXTENDEDKEY : 0;
            Send(Key(vk, sc, ext), Key(vk, sc, ext | KEYEVENTF_KEYUP));
        }

        public static void Press(ushort vk) { Send(Key(vk, (ushort)MapVirtualKey(vk, 0), 0)); }
        public static void Release(ushort vk) { Send(Key(vk, (ushort)MapVirtualKey(vk, 0), KEYEVENTF_KEYUP)); }

        public static void TypeUnicode(char c)
        {
            Send(Key(0, c, KEYEVENTF_UNICODE), Key(0, c, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP));
        }

        // Real key presses work in VM consoles, iLO/iDRAC and RDP. Characters the current
        // keyboard layout can't produce fall back to Unicode input.
        public static void TypeKey(char c)
        {
            short scan = VkKeyScan(c);
            if (scan == -1) { TypeUnicode(c); return; }

            ushort vk = (ushort)(scan & 0xFF);
            int state = (scan >> 8) & 0xFF;
            bool shift = (state & 1) != 0, ctrl = (state & 2) != 0, alt = (state & 4) != 0;
            if (shift) Press(VK_SHIFT);
            if (ctrl) Press(VK_CONTROL);
            if (alt) Press(VK_MENU);
            TapVk(vk);
            if (alt) Release(VK_MENU);
            if (ctrl) Release(VK_CONTROL);
            if (shift) Release(VK_SHIFT);
        }
    }
}
