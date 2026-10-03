using System.Collections.Generic;

namespace ClipTyper
{
    enum StepKind { Char, Enter, Tab, Left, DownEnd }

    struct Step
    {
        public StepKind Kind;
        public char C;
        public static Step Of(StepKind k) { var s = new Step(); s.Kind = k; return s; }
        public static Step Ch(char c) { var s = new Step(); s.Kind = StepKind.Char; s.C = c; return s; }
    }

    // Turns clipboard text into a list of keystroke steps.
    static class Planner
    {
        public static List<Step> Plan(string text, Settings s)
        {
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            return s.Mode == TypingMode.Code ? CodePlan(text) : ExactPlan(text, s);
        }

        // Types the text as copied. Tabs and trailing spaces follow the settings.
        public static List<Step> ExactPlan(string text, Settings s)
        {
            var steps = new List<Step>();
            string[] lines = text.Split('\n');
            for (int li = 0; li < lines.Length; li++)
            {
                if (li > 0) steps.Add(Step.Of(StepKind.Enter));
                string line = lines[li];
                int end = line.Length;
                if (s.TrimTrailingSpaces)
                    while (end > 0 && (line[end - 1] == ' ' || line[end - 1] == '\t')) end--;
                for (int i = 0; i < end; i++)
                {
                    char c = line[i];
                    if (c == '\t')
                    {
                        if (s.Tabs == TabHandling.Spaces)
                            for (int k = 0; k < s.TabSpaces; k++) steps.Add(Step.Ch(' '));
                        else
                            steps.Add(Step.Of(StepKind.Tab));
                    }
                    else steps.Add(Step.Ch(c));
                }
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
                    steps.Add(Step.Ch(c == '\t' ? ' ' : c));
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
            return i + 1 < t.Length;
        }

        static bool StartsLine(string t, int i)
        {
            for (int j = i - 1; j >= 0 && t[j] != '\n'; j--)
                if (t[j] != ' ' && t[j] != '\t') return false;
            return i > 0;
        }
    }
}
