# ClipTyper

A tiny Windows tray app that **types** your clipboard as real keystrokes instead of pasting it.

Useful anywhere paste is blocked or broken: VM consoles, iLO/iDRAC/IPMI web consoles, some RDP or VDI sessions, and editors that disable paste.

## Usage

1. Run `ClipTyper.exe`. An icon appears in the system tray.
2. Copy text as usual.
3. Click where you want it typed and press **Ctrl+Shift+V**.
4. Press **Esc** to stop typing early.

Right-click the tray icon to:

- **Code editor mode**: toggle on when typing code into an IDE (see below).
- **Typing speed**: slow it down if a laggy console drops characters.
- **Exit**.

Settings are saved to `ClipTyper.ini` next to the exe.

## Code editor mode

Code editors auto-indent new lines and auto-close braces. Typing code key by key into them normally produces a "staircase" of indentation and extra `}` at the end. With Code editor mode on, ClipTyper:

- Skips the leading whitespace on each line, so the editor's own indentation is used.
- Types a line-ending `{` as `{}`, steps between them, and presses Enter, so the editor places `}` on its own line. When the copied code reaches that `}`, it moves onto it instead of typing another one.

Turn it **off** for terminals and VM consoles, since it uses arrow keys to move around.

Known limitations:

- Empty blocks may end up with a blank line inside.
- Javadoc-style `/** ... */` comments may get doubled `*`, since editors add their own.
- If an autocomplete popup is open when Enter is pressed, the editor may accept the suggestion. A slower typing speed helps.

## Building

No SDK or Visual Studio needed. Windows ships with the .NET Framework C# compiler. Run:

```bat
build.bat
```

This produces `ClipTyper.exe` in the same folder.

## Starting with Windows

Press Win+R, type `shell:startup`, and place a shortcut to `ClipTyper.exe` in the folder that opens.
