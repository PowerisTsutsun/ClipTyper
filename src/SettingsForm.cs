using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipTyper
{
    // Text box that records a keyboard shortcut instead of text.
    class HotkeyBox : TextBox
    {
        public uint Modifiers;
        public Keys Key;

        public HotkeyBox()
        {
            ReadOnly = true;
            BackColor = SystemColors.Window;
            Cursor = Cursors.Hand;
            ShortcutsEnabled = false;
        }

        public void SetHotkey(uint mods, Keys key)
        {
            Modifiers = mods; Key = key;
            Text = Settings.FormatHotkey(mods, key);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            if (key == Keys.Tab && (keyData & Keys.Modifiers) == 0) return base.ProcessCmdKey(ref msg, keyData);
            if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu || key == Keys.LWin || key == Keys.RWin)
            {
                Text = Prefix(keyData) + "...";
                return true;
            }
            uint mods = 0;
            if ((keyData & Keys.Control) != 0) mods |= Native.MOD_CONTROL;
            if ((keyData & Keys.Shift) != 0) mods |= Native.MOD_SHIFT;
            if ((keyData & Keys.Alt) != 0) mods |= Native.MOD_ALT;
            if (mods == 0 || mods == Native.MOD_SHIFT)
            {
                Text = "Use Ctrl or Alt with a key";
                return true;
            }
            SetHotkey(mods, key);
            return true;
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            SelectionStart = Text.Length;
            SelectionLength = 0;
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Text = Settings.FormatHotkey(Modifiers, Key);
        }

        static string Prefix(Keys k)
        {
            string s = "";
            if ((k & Keys.Control) != 0) s += "Ctrl+";
            if ((k & Keys.Shift) != 0) s += "Shift+";
            if ((k & Keys.Alt) != 0) s += "Alt+";
            return s;
        }
    }

    class SettingsForm : Form
    {
        readonly Func<Settings, bool> onSave;

        HotkeyBox hotkey;
        CheckBox enabled, trim, notifications, startup;
        RadioButton exact, code;
        TrackBar speed;
        Label speedLabel;
        NumericUpDown startDelay, tabSpaces;
        ComboBox newline, tabs, input;

        public SettingsForm(Settings current, Func<Settings, bool> onSave)
        {
            this.onSave = onSave;

            Text = "ClipTyper Settings";
            Icon = TrayApp.AppIcon;
            Font = new Font("Segoe UI", 9f);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(12);

            var root = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill };
            Controls.Add(root);

            // Shortcut
            var g1 = Group("Shortcut");
            var t1 = Grid(g1, 2);
            t1.Controls.Add(Lbl("Type clipboard with:"), 0, 0);
            hotkey = new HotkeyBox { Width = 200 };
            t1.Controls.Add(hotkey, 1, 0);
            t1.Controls.Add(Hint("Click the box, then press the shortcut you want."), 1, 1);
            enabled = new CheckBox { Text = "Enabled", AutoSize = true };
            t1.Controls.Add(enabled, 1, 2);
            root.Controls.Add(g1);

            // Mode
            var g2 = Group("Typing mode");
            var f2 = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
            exact = new RadioButton { Text = "Exact: type the text exactly as copied", AutoSize = true };
            f2.Controls.Add(exact);
            f2.Controls.Add(Hint("Best for terminals, VM consoles, remote desktops and plain text boxes.", 18));
            code = new RadioButton { Text = "Code editor: work with auto-indent and auto-closing braces", AutoSize = true };
            f2.Controls.Add(code);
            f2.Controls.Add(Hint("Best for IDEs like VS Code and IntelliJ. Your editor handles indentation.", 18));
            g2.Controls.Add(f2);
            root.Controls.Add(g2);

            // Speed
            var g3 = Group("Speed");
            var t3 = Grid(g3, 2);
            t3.Controls.Add(Lbl("Delay between keys:"), 0, 0);
            var speedRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            speed = new TrackBar { Minimum = 0, Maximum = 200, TickFrequency = 25, SmallChange = 5, LargeChange = 25, Width = 220, AutoSize = false, Height = 32 };
            speedLabel = new Label { AutoSize = true, Margin = new Padding(4, 8, 0, 0) };
            speed.ValueChanged += delegate { speedLabel.Text = speed.Value + " ms" + (speed.Value == 0 ? " (fastest)" : ""); };
            speedRow.Controls.Add(speed);
            speedRow.Controls.Add(speedLabel);
            t3.Controls.Add(speedRow, 1, 0);
            t3.Controls.Add(Hint("Raise this if characters go missing in slow or remote windows."), 1, 1);
            t3.Controls.Add(Lbl("Wait before typing:"), 0, 2);
            var delayRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            startDelay = new NumericUpDown { Minimum = 0, Maximum = 10000, Increment = 50, Width = 80 };
            delayRow.Controls.Add(startDelay);
            delayRow.Controls.Add(new Label { Text = "ms", AutoSize = true, Margin = new Padding(4, 6, 0, 0) });
            t3.Controls.Add(delayRow, 1, 2);
            root.Controls.Add(g3);

            // Keys
            var g4 = Group("How text is sent");
            var t4 = Grid(g4, 2);
            t4.Controls.Add(Lbl("New lines:"), 0, 0);
            newline = Combo("Enter", "Shift+Enter (chat apps that send on Enter)");
            t4.Controls.Add(newline, 1, 0);
            t4.Controls.Add(Lbl("Tabs:"), 0, 1);
            var tabRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            tabs = Combo("Press the Tab key", "Convert to spaces");
            tabs.Width = 160;
            tabSpaces = new NumericUpDown { Minimum = 1, Maximum = 16, Width = 50 };
            tabs.SelectedIndexChanged += delegate { tabSpaces.Enabled = tabs.SelectedIndex == 1; };
            tabRow.Controls.Add(tabs);
            tabRow.Controls.Add(tabSpaces);
            tabRow.Controls.Add(new Label { Text = "spaces", AutoSize = true, Margin = new Padding(4, 6, 0, 0) });
            t4.Controls.Add(tabRow, 1, 1);
            t4.Controls.Add(Lbl("Input method:"), 0, 2);
            input = Combo("Real key presses (works in consoles and RDP)", "Unicode characters (exact symbols, accents, emoji)");
            t4.Controls.Add(input, 1, 2);
            trim = new CheckBox { Text = "Remove spaces at the end of lines", AutoSize = true };
            t4.Controls.Add(trim, 1, 3);
            t4.Controls.Add(Hint("Tabs and trailing spaces apply to Exact mode. Code editor mode lets the editor indent."), 1, 4);
            root.Controls.Add(g4);

            // General
            var g5 = Group("General");
            var f5 = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
            startup = new CheckBox { Text = "Start ClipTyper when Windows starts", AutoSize = true };
            notifications = new CheckBox { Text = "Show notifications", AutoSize = true };
            f5.Controls.Add(startup);
            f5.Controls.Add(notifications);
            g5.Controls.Add(f5);
            root.Controls.Add(g5);

            // Try it
            var g6 = Group("Try it");
            var f6 = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
            f6.Controls.Add(Hint("Save, copy some text, click in the box below and press your shortcut.", 0));
            var test = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsTab = true, AcceptsReturn = true, Width = 470, Height = 70, Font = new Font("Consolas", 9f) };
            f6.Controls.Add(test);
            g6.Controls.Add(f6);
            root.Controls.Add(g6);

            // Buttons
            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 0) };
            var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
            var save = new Button { Text = "Save", AutoSize = true };
            var defaults = new Button { Text = "Restore defaults", AutoSize = true };
            save.Click += delegate { if (Save()) Close(); };
            cancel.Click += delegate { Close(); };
            defaults.Click += delegate { LoadValues(new Settings(), false); };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(save);
            buttons.Controls.Add(defaults);
            root.Controls.Add(buttons);

            AcceptButton = save;
            CancelButton = cancel;

            LoadValues(current, Settings.StartWithWindows);
        }

        void LoadValues(Settings s, bool startWithWindows)
        {
            hotkey.SetHotkey(s.HotkeyModifiers, s.HotkeyKey);
            enabled.Checked = s.Enabled;
            exact.Checked = s.Mode == TypingMode.Exact;
            code.Checked = s.Mode == TypingMode.Code;
            speed.Value = Math.Min(speed.Maximum, Math.Max(0, s.DelayMs));
            speedLabel.Text = speed.Value + " ms" + (speed.Value == 0 ? " (fastest)" : "");
            startDelay.Value = Math.Min(startDelay.Maximum, Math.Max(0, s.StartDelayMs));
            newline.SelectedIndex = (int)s.Newline;
            tabs.SelectedIndex = (int)s.Tabs;
            tabSpaces.Value = Math.Min(16, Math.Max(1, s.TabSpaces));
            tabSpaces.Enabled = s.Tabs == TabHandling.Spaces;
            input.SelectedIndex = (int)s.Input;
            trim.Checked = s.TrimTrailingSpaces;
            notifications.Checked = s.ShowNotifications;
            startup.Checked = startWithWindows;
        }

        bool Save()
        {
            var s = new Settings
            {
                HotkeyModifiers = hotkey.Modifiers,
                HotkeyKey = hotkey.Key,
                Enabled = enabled.Checked,
                Mode = code.Checked ? TypingMode.Code : TypingMode.Exact,
                DelayMs = speed.Value,
                StartDelayMs = (int)startDelay.Value,
                Newline = (NewlineKey)newline.SelectedIndex,
                Tabs = (TabHandling)tabs.SelectedIndex,
                TabSpaces = (int)tabSpaces.Value,
                Input = (InputMethod)input.SelectedIndex,
                TrimTrailingSpaces = trim.Checked,
                ShowNotifications = notifications.Checked,
            };
            if (!onSave(s)) return false;
            if (startup.Checked != Settings.StartWithWindows) Settings.StartWithWindows = startup.Checked;
            return true;
        }

        static GroupBox Group(string title)
        {
            return new GroupBox
            {
                Text = title, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill, Padding = new Padding(10, 6, 10, 8), Margin = new Padding(0, 0, 0, 8),
                MinimumSize = new Size(500, 0)
            };
        }

        static TableLayoutPanel Grid(GroupBox g, int cols)
        {
            var t = new TableLayoutPanel { ColumnCount = cols, AutoSize = true, Dock = DockStyle.Fill };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            g.Controls.Add(t);
            return t;
        }

        static Label Lbl(string text)
        {
            return new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 8, 0) };
        }

        static Label Hint(string text, int indent = 0)
        {
            return new Label
            {
                Text = text, AutoSize = true, ForeColor = SystemColors.GrayText,
                Margin = new Padding(indent, 0, 0, 4), MaximumSize = new Size(470, 0)
            };
        }

        static ComboBox Combo(params string[] items)
        {
            var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };
            c.Items.AddRange(items);
            return c;
        }
    }
}
