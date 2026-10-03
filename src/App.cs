using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;

namespace ClipTyper
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool created;
            using (var mutex = new Mutex(true, "ClipTyper_SingleInstance", out created))
            using (var showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, "ClipTyper_ShowSettings"))
            {
                if (!created)
                {
                    // Already running: ask that copy to open its Settings window.
                    showSignal.Set();
                    return;
                }
                try { Native.SetProcessDPIAware(); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApp(showSignal));
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

    class TrayApp : ApplicationContext
    {
        const int HOTKEY_ID = 0xC11B;

        readonly NotifyIcon tray;
        readonly HotkeyWindow hotkeyWindow;
        readonly Control ui; // used to marshal calls back onto the UI thread
        readonly ToolStripMenuItem enabledItem, exactItem, codeItem;
        Settings settings;
        SettingsForm settingsForm;
        volatile bool typing;
        bool hotkeyRegistered;

        public static Icon AppIcon = MakeIcon();

        public TrayApp(EventWaitHandle showSignal)
        {
            bool firstRun = !Settings.Exists;
            settings = Settings.Load();

            ui = new Control();
            ui.CreateControl();

            hotkeyWindow = new HotkeyWindow();
            hotkeyWindow.HotkeyPressed += OnHotkey;

            var menu = new ContextMenuStrip();
            var open = new ToolStripMenuItem("Settings...", null, delegate { ShowSettings(); });
            open.Font = new Font(open.Font, FontStyle.Bold);
            menu.Items.Add(open);
            menu.Items.Add(new ToolStripSeparator());

            exactItem = new ToolStripMenuItem("Exact mode", null, delegate { SetMode(TypingMode.Exact); });
            codeItem = new ToolStripMenuItem("Code editor mode", null, delegate { SetMode(TypingMode.Code); });
            menu.Items.Add(exactItem);
            menu.Items.Add(codeItem);
            menu.Items.Add(new ToolStripSeparator());

            enabledItem = new ToolStripMenuItem("Enabled", null, delegate
            {
                settings.Enabled = !settings.Enabled;
                settings.Save();
                ApplySettings(false);
            });
            menu.Items.Add(enabledItem);
            menu.Items.Add("Exit", null, delegate { ExitThread(); });

            tray = new NotifyIcon();
            tray.Icon = AppIcon;
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { ShowSettings(); };
            tray.BalloonTipClicked += delegate { ShowSettings(); };
            tray.Visible = true;

            ApplySettings(false);
            if (hotkeyRegistered && settings.ShowNotifications)
                Notify("ClipTyper is running",
                    "Copy text, then press " + settings.HotkeyText + " to type it. Esc stops typing. Double-click the tray icon for settings.");

            // Launching ClipTyper.exe again opens Settings in this copy.
            var waiter = new Thread(delegate()
            {
                while (true)
                {
                    showSignal.WaitOne();
                    try { ui.BeginInvoke((Action)ShowSettings); } catch { return; }
                }
            });
            waiter.IsBackground = true;
            waiter.Start();

            if (firstRun)
            {
                settings.Save();
                ShowSettings();
            }
        }

        void SetMode(TypingMode m)
        {
            settings.Mode = m;
            settings.Save();
            ApplySettings(false);
        }

        // Re-registers the hotkey and refreshes the menu. Returns false if the hotkey is taken.
        public bool ApplySettings(bool reportErrors)
        {
            if (hotkeyRegistered) Native.UnregisterHotKey(hotkeyWindow.Handle, HOTKEY_ID);
            hotkeyRegistered = false;

            bool ok = true;
            if (settings.Enabled)
            {
                hotkeyRegistered = Native.RegisterHotKey(hotkeyWindow.Handle, HOTKEY_ID,
                    settings.HotkeyModifiers | Native.MOD_NOREPEAT, (uint)settings.HotkeyKey);
                ok = hotkeyRegistered;
                if (!ok && reportErrors)
                    MessageBox.Show(settings.HotkeyText + " is already used by another program. Pick a different shortcut.",
                        "ClipTyper", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                else if (!ok)
                    Notify("Shortcut unavailable", settings.HotkeyText + " is used by another program. Open Settings to pick another.");
            }

            enabledItem.Checked = settings.Enabled;
            exactItem.Checked = settings.Mode == TypingMode.Exact;
            codeItem.Checked = settings.Mode == TypingMode.Code;
            SetTrayText(null);
            return ok;
        }

        void SetTrayText(string status)
        {
            string t = status ?? (settings.Enabled
                ? "ClipTyper: " + settings.HotkeyText + (settings.Mode == TypingMode.Code ? " (code mode)" : "")
                : "ClipTyper: disabled");
            tray.Text = t.Length > 63 ? t.Substring(0, 63) : t;
        }

        void Notify(string title, string text)
        {
            tray.ShowBalloonTip(4000, title, text, ToolTipIcon.Info);
        }

        void ShowSettings()
        {
            if (settingsForm != null && !settingsForm.IsDisposed)
            {
                settingsForm.WindowState = FormWindowState.Normal;
                settingsForm.Activate();
                return;
            }
            settingsForm = new SettingsForm(settings, delegate(Settings updated)
            {
                var old = settings;
                settings = updated;
                settings.Save();
                if (!ApplySettings(true))
                {
                    settings = old;
                    settings.Save();
                    ApplySettings(false);
                    return false;
                }
                return true;
            });
            settingsForm.Show();
            settingsForm.Activate();
        }

        void OnHotkey()
        {
            if (typing) return;
            string text = null;
            try { if (Clipboard.ContainsText()) text = Clipboard.GetText(); } catch { }
            if (string.IsNullOrEmpty(text)) return;

            var snapshot = settings.Clone();
            var steps = Planner.Plan(text, snapshot);
            if (steps.Count == 0) return;

            typing = true;
            var t = new Thread(delegate()
            {
                bool done = false;
                try
                {
                    done = Typer.Run(steps, snapshot, delegate(int pct)
                    {
                        ui.BeginInvoke((Action)delegate { SetTrayText("ClipTyper: typing " + pct + "% (Esc to stop)"); });
                    });
                }
                finally
                {
                    typing = false;
                    ui.BeginInvoke((Action)delegate
                    {
                        SetTrayText(null);
                        if (!done && snapshot.ShowNotifications) Notify("Typing stopped", "Esc was pressed, so ClipTyper stopped typing.");
                    });
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        protected override void ExitThreadCore()
        {
            if (hotkeyRegistered) Native.UnregisterHotKey(hotkeyWindow.Handle, HOTKEY_ID);
            hotkeyWindow.Dispose();
            if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.Close();
            tray.Visible = false;
            tray.Dispose();
            base.ExitThreadCore();
        }

        // Draws a small keyboard icon so the app doesn't use the generic Windows one.
        static Icon MakeIcon()
        {
            using (var bmp = new Bitmap(32, 32))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (var path = RoundedRect(new RectangleF(1, 6, 30, 21), 5))
                    using (var fill = new SolidBrush(Color.FromArgb(37, 99, 235)))
                        g.FillPath(fill, path);
                    using (var key = new SolidBrush(Color.White))
                    {
                        for (int row = 0; row < 2; row++)
                            for (int col = 0; col < 5; col++)
                                g.FillRectangle(key, 5 + col * 5, 10 + row * 5, 3, 3);
                        g.FillRectangle(key, 9, 20, 14, 3);
                    }
                }
                IntPtr h = bmp.GetHicon();
                var icon = (Icon)Icon.FromHandle(h).Clone();
                Native.DestroyIcon(h);
                return icon;
            }
        }

        static GraphicsPath RoundedRect(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
