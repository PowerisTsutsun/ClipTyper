using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ClipTyper
{
    enum TypingMode { Exact = 0, Code = 1 }
    enum NewlineKey { Enter = 0, ShiftEnter = 1 }
    enum TabHandling { TabKey = 0, Spaces = 1 }
    enum InputMethod { KeyPresses = 0, Unicode = 1 }

    class Settings
    {
        public uint HotkeyModifiers = Native.MOD_CONTROL | Native.MOD_SHIFT;
        public Keys HotkeyKey = Keys.V;
        public bool Enabled = true;
        public TypingMode Mode = TypingMode.Exact;
        public int DelayMs = 15;
        public int StartDelayMs = 150;
        public NewlineKey Newline = NewlineKey.Enter;
        public TabHandling Tabs = TabHandling.TabKey;
        public int TabSpaces = 4;
        public InputMethod Input = InputMethod.KeyPresses;
        public bool TrimTrailingSpaces = true;
        public bool ShowNotifications = true;

        static string Dir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClipTyper"); }
        }
        static string FilePath { get { return Path.Combine(Dir, "settings.ini"); } }

        public static bool Exists { get { return File.Exists(FilePath); } }

        public Settings Clone() { return (Settings)MemberwiseClone(); }

        public string HotkeyText { get { return FormatHotkey(HotkeyModifiers, HotkeyKey); } }

        public static string FormatHotkey(uint mods, Keys key)
        {
            var parts = new List<string>();
            if ((mods & Native.MOD_CONTROL) != 0) parts.Add("Ctrl");
            if ((mods & Native.MOD_SHIFT) != 0) parts.Add("Shift");
            if ((mods & Native.MOD_ALT) != 0) parts.Add("Alt");
            if ((mods & Native.MOD_WIN) != 0) parts.Add("Win");
            parts.Add(KeyName(key));
            return string.Join("+", parts);
        }

        public static string KeyName(Keys key)
        {
            if (key >= Keys.D0 && key <= Keys.D9) return ((int)(key - Keys.D0)).ToString();
            switch (key)
            {
                case Keys.Oemtilde: return "`";
                case Keys.OemMinus: return "-";
                case Keys.Oemplus: return "=";
                case Keys.OemOpenBrackets: return "[";
                case Keys.OemCloseBrackets: return "]";
                case Keys.OemPipe: return "\\";
                case Keys.OemSemicolon: return ";";
                case Keys.OemQuotes: return "'";
                case Keys.Oemcomma: return ",";
                case Keys.OemPeriod: return ".";
                case Keys.OemQuestion: return "/";
                default: return key.ToString();
            }
        }

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                foreach (var raw in File.ReadAllLines(FilePath))
                {
                    int eq = raw.IndexOf('=');
                    if (eq < 0) continue;
                    string k = raw.Substring(0, eq).Trim(), v = raw.Substring(eq + 1).Trim();
                    int n;
                    bool isInt = int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out n);
                    switch (k)
                    {
                        case "hotkeyModifiers": if (isInt) s.HotkeyModifiers = (uint)n; break;
                        case "hotkeyKey": if (isInt) s.HotkeyKey = (Keys)n; break;
                        case "enabled": s.Enabled = v == "1"; break;
                        case "mode": if (isInt) s.Mode = (TypingMode)n; break;
                        case "delayMs": if (isInt) s.DelayMs = Clamp(n, 0, 1000); break;
                        case "startDelayMs": if (isInt) s.StartDelayMs = Clamp(n, 0, 10000); break;
                        case "newline": if (isInt) s.Newline = (NewlineKey)n; break;
                        case "tabs": if (isInt) s.Tabs = (TabHandling)n; break;
                        case "tabSpaces": if (isInt) s.TabSpaces = Clamp(n, 1, 16); break;
                        case "input": if (isInt) s.Input = (InputMethod)n; break;
                        case "trimTrailingSpaces": s.TrimTrailingSpaces = v == "1"; break;
                        case "showNotifications": s.ShowNotifications = v == "1"; break;
                    }
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllLines(FilePath, new[]
                {
                    "hotkeyModifiers=" + HotkeyModifiers,
                    "hotkeyKey=" + (int)HotkeyKey,
                    "enabled=" + B(Enabled),
                    "mode=" + (int)Mode,
                    "delayMs=" + DelayMs,
                    "startDelayMs=" + StartDelayMs,
                    "newline=" + (int)Newline,
                    "tabs=" + (int)Tabs,
                    "tabSpaces=" + TabSpaces,
                    "input=" + (int)Input,
                    "trimTrailingSpaces=" + B(TrimTrailingSpaces),
                    "showNotifications=" + B(ShowNotifications),
                });
            }
            catch { }
        }

        static string B(bool b) { return b ? "1" : "0"; }
        static int Clamp(int v, int lo, int hi) { return v < lo ? lo : v > hi ? hi : v; }

        // "Start with Windows" lives in the per-user Run key rather than the settings file.
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool StartWithWindows
        {
            get
            {
                try
                {
                    using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                        return k != null && k.GetValue("ClipTyper") != null;
                }
                catch { return false; }
            }
            set
            {
                try
                {
                    using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                    {
                        if (value) k.SetValue("ClipTyper", "\"" + Application.ExecutablePath + "\"");
                        else if (k.GetValue("ClipTyper") != null) k.DeleteValue("ClipTyper");
                    }
                }
                catch { }
            }
        }
    }
}
