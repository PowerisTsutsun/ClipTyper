// ClipTyper: press Ctrl+Shift+V to type the clipboard text as keystrokes.
// Press Esc while typing to abort. Right-click the tray icon to change speed, toggle
// Code editor mode, or exit.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static void Main()
    {
        bool created;
        using (var mutex = new Mutex(true, "ClipTyper_SingleInstance", out created))
        {
            if (!created)
            {
                MessageBox.Show("ClipTyper is already running.", "ClipTyper");
                return;
            }
            Application.EnableVisualStyles();
            Application.Run(new TyperContext());
        }
    }
}

class HotkeyWindow : NativeWindow, IDisposable
{
    public event Action HotkeyPressed;
    const int WM_HOTKEY = 0x0312;
    public HotkeyWindow() { CreateHandle(new CreateParams()); }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && HotkeyPressed != null) HotkeyPressed();
        base.WndProc(ref m);
    }
    public void Dispose() { DestroyHandle(); }
}

class TyperContext : ApplicationContext
{
    const int HOTKEY_ID = 0xC11B;
    const uint MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_NOREPEAT = 0x4000;
    const uint VK_V = 0x56;

    readonly NotifyIcon tray;
    readonly HotkeyWindow hotkeyWindow;
    readonly string settingsPath;
    volatile bool typing;
    int delayMs = 15;
    bool codeMode = true;

    public TyperContext()
    {
        settingsPath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "ClipTyper.ini");
        LoadSettings();

        hotkeyWindow = new HotkeyWindow();
        hotkeyWindow.HotkeyPressed += OnHotkey;

        tray = new NotifyIcon();
        tray.Icon = SystemIcons.Application;
        tray.Text = "ClipTyper (Ctrl+Shift+V)";
        tray.Visible = true;

        var menu = new ContextMenuStrip();
        var code = new ToolStripMenuItem("Code editor mode");
        code.Checked = codeMode;
        code.Click += delegate { codeMode = !codeMode; code.Checked = codeMode; SaveSettings(); };
        menu.Items.Add(code);

        var speed = new ToolStripMenuItem("Typing speed");
        AddSpeed(speed, "Fast (5 ms)", 5);
        AddSpeed(speed, "Normal (15 ms)", 15);
        AddSpeed(speed, "Slow (40 ms)", 40);
        AddSpeed(speed, "Very slow, for laggy consoles (100 ms)", 100);
        menu.Items.Add(speed);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, delegate { ExitThread(); });
        tray.ContextMenuStrip = menu;

        if (!Native.RegisterHotKey(hotkeyWindow.Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, VK_V))
        {
            MessageBox.Show("Could not register Ctrl+Shift+V. Another program may be using it.", "ClipTyper");
        }
        else
        {
            tray.ShowBalloonTip(3000, "ClipTyper running",
                "Copy text, then press Ctrl+Shift+V to type it. Esc stops typing.", ToolTipIcon.Info);
        }
    }

    void LoadSettings()
    {
        try
        {
            if (!File.Exists(settingsPath)) return;
            foreach (var line in File.ReadAllLines(settingsPath))
            {
                var parts = line.Split('=');
                if (parts.Length != 2) continue;
                if (parts[0].Trim() == "codeMode") codeMode = parts[1].Trim() == "1";
                if (parts[0].Trim() == "delayMs") { int d; if (int.TryParse(parts[1].Trim(), out d)) delayMs = d; }
            }
        }
        catch { }
    }

    void SaveSettings()
    {
        try { File.WriteAllLines(settingsPath, new[] { "codeMode=" + (codeMode ? "1" : "0"), "delayMs=" + delayMs }); }
        catch { }
    }

    void AddSpeed(ToolStripMenuItem parent, string label, int ms)
    {
        var item = new ToolStripMenuItem(label);
        item.Checked = ms == delayMs;
        item.Click += delegate
        {
            delayMs = ms;
            foreach (ToolStripMenuItem i in parent.DropDownItems) i.Checked = (i == item);
            SaveSettings();
        };
        parent.DropDownItems.Add(item);
    }

    void OnHotkey()
    {
        if (typing) return;
        string text = null;
        try { if (Clipboard.ContainsText()) text = Clipboard.GetText(); } catch { }
        if (string.IsNullOrEmpty(text)) return;

        text = text.Replace("\r\n", "\n").Replace("\r", "\n");
        List<Step> steps = codeMode ? Planner.CodePlan(text) : Planner.ExactPlan(text);
        int delay = delayMs;
        typing = true;
        var t = new Thread(delegate()
        {
            try { Run(steps, delay); }
            finally { typing = false; }
        });
        t.IsBackground = true;
        t.Start();
    }

    static void Run(List<Step> steps, int delay)
    {
        // Wait until the user lets go of Ctrl/Shift/Alt/Win/V so they don't mix with typed keys.
        int waited = 0;
        while (waited < 3000 && (Native.IsDown(0x10) || Native.IsDown(0x11) || Native.IsDown(0x12) ||
                                 Native.IsDown(0x5B) || Native.IsDown(0x5C) || Native.IsDown(0x56)))
        {
            Thread.Sleep(20); waited += 20;
        }
        Thread.Sleep(100);

        foreach (var s in steps)
        {
            if (Native.IsDown(0x1B)) return; // Esc aborts
            switch (s.Kind)
            {
                case StepKind.Char: Native.TypeChar(s.C); break;
                case StepKind.Enter: Native.TapVk(0x0D); break;
                case StepKind.Tab: Native.TapVk(0x09); break;
                case StepKind.Left: Native.TapVk(0x25, true); break;
                case StepKind.DownEnd:
                    Native.TapVk(0x28, true);
                    if (delay > 0) Thread.Sleep(delay);
                    Native.TapVk(0x23, true);
                    break;
            }
            if (delay > 0) Thread.Sleep(delay);
        }
    }

    protected override void ExitThreadCore()
    {
        Native.UnregisterHotKey(hotkeyWindow.Handle, HOTKEY_ID);
        hotkeyWindow.Dispose();
        tray.Visible = false;
        tray.Dispose();
        base.ExitThreadCore();
    }
}

enum StepKind { Char, Enter, Tab, Left, DownEnd }

struct Step
{
    public StepKind Kind; public char C;
    public static Step Of(StepKind k) { var s = new Step(); s.Kind = k; return s; }
    public static Step Ch(char c) { var s = new Step(); s.Kind = StepKind.Char; s.C = c; return s; }
}

static class Planner
{
    // Types the text exactly as copied.
    public static List<Step> ExactPlan(string text)
    {
        var steps = new List<Step>();
        foreach (char c in text)
        {
            if (c == '\n') steps.Add(Step.Of(StepKind.Enter));
            else if (c == '\t') steps.Add(Step.Of(StepKind.Tab));
            else steps.Add(Step.Ch(c));
        }
        return steps;
    }

    // Types code so that an editor's auto-indent and auto-closing braces give the right result:
    //  - Leading whitespace on each line is skipped; the editor indents.
    //  - A '{' that ends a line, whose matching '}' starts a later line, is typed as "{}" + Left,
    //    then Enter. The editor puts '}' on its own line below. When the source reaches that
    //    '}', we move Down + End onto it instead of typing another one.
    public static List<Step> CodePlan(string text)
    {
        var autoOpeners = new HashSet<int>();
        var autoClosers = new HashSet<int>();
        FindBlockBraces(text, autoOpeners, autoClosers);

        var steps = new List<Step>();
        int pos = 0;
        string[] lines = text.Split('\n');
        for (int li = 0; li < lines.Length; li++)
        {
            string line = lines[li];
            int start = 0;
            int end = line.Length;
            while (end > 0 && (line[end - 1] == ' ' || line[end - 1] == '\t')) end--;

            if (li > 0)
            {
                while (start < end && (line[start] == ' ' || line[start] == '\t')) start++;
                if (start < end && autoClosers.Contains(pos + start))
                {
                    steps.Add(Step.Of(StepKind.DownEnd));
                    start++;
                }
                else
                {
                    steps.Add(Step.Of(StepKind.Enter));
                }
            }

            for (int i = start; i < end; i++)
            {
                char c = line[i];
                if (c == '\t') steps.Add(Step.Ch(' '));
                else steps.Add(Step.Ch(c));
                if (autoOpeners.Contains(pos + i))
                {
                    steps.Add(Step.Ch('}'));
                    steps.Add(Step.Of(StepKind.Left));
                }
            }
            pos += line.Length + 1;
        }
        return steps;
    }

    // Pairs up { } outside of strings and comments, and records the pairs where '{' is the
    // last thing on its line and the matching '}' is the first thing on a later line.
    static void FindBlockBraces(string t, HashSet<int> openers, HashSet<int> closers)
    {
        var stack = new Stack<int>();
        int n = t.Length;
        for (int i = 0; i < n; i++)
        {
            char c = t[i];
            if (c == '/' && i + 1 < n && t[i + 1] == '/') { while (i < n && t[i] != '\n') i++; continue; }
            if (c == '/' && i + 1 < n && t[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < n && !(t[i] == '*' && t[i + 1] == '/')) i++;
                i++;
                continue;
            }
            if (c == '"' || c == '\'' || c == '`')
            {
                char q = c; i++;
                while (i < n && t[i] != q && t[i] != '\n') { if (t[i] == '\\') i++; i++; }
                continue;
            }
            if (c == '{') stack.Push(i);
            else if (c == '}' && stack.Count > 0)
            {
                int o = stack.Pop();
                if (EndsLine(t, o) && StartsLine(t, i))
                {
                    openers.Add(o);
                    closers.Add(i);
                }
            }
        }
    }

    static bool EndsLine(string t, int i)
    {
        for (int j = i + 1; j < t.Length && t[j] != '\n'; j++)
            if (t[j] != ' ' && t[j] != '\t') return false;
        return i + 1 < t.Length; // something must follow, or there's no block body
    }

    static bool StartsLine(string t, int i)
    {
        for (int j = i - 1; j >= 0 && t[j] != '\n'; j--)
            if (t[j] != ' ' && t[j] != '\t') return false;
        return i > 0;
    }
}

static class Native
{
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
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

    static void Press(ushort vk) { Send(Key(vk, (ushort)MapVirtualKey(vk, 0), 0)); }
    static void Release(ushort vk) { Send(Key(vk, (ushort)MapVirtualKey(vk, 0), KEYEVENTF_KEYUP)); }

    // Prefer real key presses (works in VM consoles, iLO/iDRAC, RDP); fall back to Unicode input.
    public static void TypeChar(char c)
    {
        short scan = VkKeyScan(c);
        if (scan != -1)
        {
            ushort vk = (ushort)(scan & 0xFF);
            int shiftState = (scan >> 8) & 0xFF;
            bool shift = (shiftState & 1) != 0, ctrl = (shiftState & 2) != 0, alt = (shiftState & 4) != 0;
            if (shift) Press(0x10);
            if (ctrl) Press(0x11);
            if (alt) Press(0x12);
            TapVk(vk);
            if (alt) Release(0x12);
            if (ctrl) Release(0x11);
            if (shift) Release(0x10);
        }
        else
        {
            Send(Key(0, c, KEYEVENTF_UNICODE), Key(0, c, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP));
        }
    }
}
