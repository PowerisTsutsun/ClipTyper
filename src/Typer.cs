using System;
using System.Collections.Generic;
using System.Threading;

namespace ClipTyper
{
    // Sends a keystroke plan. Runs on a background thread; Esc aborts.
    static class Typer
    {
        public static bool Run(List<Step> steps, Settings s, Action<int> progress)
        {
            // Wait until the user lets go of the shortcut so held modifiers don't mix with typed keys.
            int waited = 0;
            while (waited < 3000 && (AnyModifierDown() || Native.IsDown((int)s.HotkeyKey)))
            {
                Thread.Sleep(20); waited += 20;
            }
            Thread.Sleep(Math.Max(0, s.StartDelayMs));

            int lastPct = -1;
            for (int i = 0; i < steps.Count; i++)
            {
                if (Native.IsDown(Native.VK_ESCAPE)) return false;
                var st = steps[i];
                switch (st.Kind)
                {
                    case StepKind.Char:
                        if (s.Input == InputMethod.Unicode) Native.TypeUnicode(st.C);
                        else Native.TypeKey(st.C);
                        break;
                    case StepKind.Enter:
                        if (s.Newline == NewlineKey.ShiftEnter)
                        {
                            Native.Press(Native.VK_SHIFT);
                            Native.TapVk(Native.VK_RETURN);
                            Native.Release(Native.VK_SHIFT);
                        }
                        else Native.TapVk(Native.VK_RETURN);
                        break;
                    case StepKind.Tab:
                        Native.TapVk(Native.VK_TAB);
                        break;
                    case StepKind.Left:
                        Native.TapVk(Native.VK_LEFT, true);
                        break;
                    case StepKind.DownEnd:
                        Native.TapVk(Native.VK_DOWN, true);
                        if (s.DelayMs > 0) Thread.Sleep(s.DelayMs);
                        Native.TapVk(Native.VK_END, true);
                        break;
                }
                if (s.DelayMs > 0) Thread.Sleep(s.DelayMs);

                int pct = (i + 1) * 100 / steps.Count;
                if (pct != lastPct && progress != null) { lastPct = pct; progress(pct); }
            }
            return true;
        }

        static bool AnyModifierDown()
        {
            return Native.IsDown(Native.VK_SHIFT) || Native.IsDown(Native.VK_CONTROL) || Native.IsDown(Native.VK_MENU) ||
                   Native.IsDown(Native.VK_LWIN) || Native.IsDown(Native.VK_RWIN);
        }
    }
}
