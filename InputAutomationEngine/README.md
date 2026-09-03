# Input Abstraction & Workflow Automation Engine

A standalone, driver-free .NET 8 console application that intercepts keyboard input and
Xbox controller input system-wide and maps them to actions (launch a process, open a URL,
send a synthetic key chord, or run a built-in system command), with rules that can switch
automatically based on which application is focused.

No kernel driver (no Interception), no DPI/sensor manipulation. Keyboard interception uses
a user-space `WH_KEYBOARD_LL` hook; controller input is polled via `xinput1_4.dll`.

## Running

```
dotnet run --project InputAutomationEngine
```

On first launch it generates `profiles.json` next to the executable (see
`profiles.sample.json` for a fully worked example). Edit it and restart the app to pick up
changes — profiles are loaded once at startup.

## profiles.json schema

```jsonc
{
  "Profiles": [
    {
      "ProcessTarget": "*",              // "*" = global/fallback, or an exe name e.g. "chrome.exe"
      "KeyMappings": [
        {
          "Key": "F13",                  // see VirtualKeyMap.cs for supported names
          "Suppress": true,              // true = swallow the physical keystroke
          "Action": { "Type": "RunProcess", "Value": "notepad.exe", "Arguments": null }
        }
      ],
      "ControllerMappings": [
        { "Button": "A", "Action": { "Type": "OpenUrl", "Value": "https://github.com" } }
      ]
    }
  ]
}
```

`ProcessTarget` matching is exact and case-insensitive; a process-specific profile fully
replaces the global one rather than merging with it. A profile with no mapping for a given
exe still falls back to whichever profile matches — usually `"*"`.

### Action.Type values

| Type | Value meaning |
|---|---|
| `RunProcess` | Executable path or command; `Arguments` is optional |
| `OpenUrl` | URL, opened via the default browser |
| `SendKeys` | Key chord(s), e.g. `"Ctrl+Shift+Escape"`; comma-separate for a sequence |
| `SystemCommand` | One of `Lock`, `VolumeUp`, `VolumeDown`, `VolumeMute`, `MediaPlayPause`, `MediaNextTrack`, `MediaPrevTrack` |

### Controller.Button values

`A`, `B`, `X`, `Y`, `LeftShoulder`, `RightShoulder`, `Start`, `Back`, `LeftThumb`,
`RightThumb`, `DPadUp`, `DPadDown`, `DPadLeft`, `DPadRight`.

Default global bindings (generated on first run): `A` → `cmd.exe`, `B` → Windows Terminal,
`X` → Stack Overflow, `Y` → GitHub.

## Notes

- Run unelevated (`asInvoker`). Running as admin would prevent the hook from seeing input
  from normal-integrity foreground windows (UIPI).
- The keyboard hook callback offloads action execution to the thread pool so it always
  returns fast enough that Windows won't silently detach it.
- Ctrl+C, console window close, and logoff/shutdown are all handled to cleanly unhook
  before the process exits.
