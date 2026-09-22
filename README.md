# Overlight

An AI-native desktop shell layered on top of a user's existing Windows
install. See [`docs/DESIGN.md`](docs/DESIGN.md) for the full framework
this codebase follows: read existing OS state, present a curated view of
it, and the only mutation allowed is toggling the OS's own Focus Assist.

## Status

Early scaffold. The three-layer architecture from the design doc exists
as a skeleton with working P/Invoke and WinRT wiring for the well-defined
pieces, and explicitly-flagged TODOs for the pieces that need to be
verified against a real Windows install before they can be trusted:

- `ReadLayer/WindowEnumerator.cs` — `EnumWindows` + `DwmGetWindowAttribute`
  window list. Should work as written.
- `ReadLayer/NotificationReader.cs` — `UserNotificationListener` wrapper.
  Needs the app packaged (MSIX or a sparse package) before
  `RequestAccessAsync` will succeed; the `.csproj` currently builds
  unpackaged for a fast dev loop.
- `Presentation/PowerModeMonitor.cs` — `PowerRegisterForEffectivePowerModeNotifications`
  wrapper. Should work as written.
- `Presentation/OcclusionMonitor.cs` — window-subclass + `WM_WINDOWPOSCHANGED`
  technique for push-driven cloak detection. Needs on-device verification
  that this message reliably fires for the cloak transitions we care
  about (virtual-desktop switch, minimize, UWP suspend).
- `Presentation/AmbientCompositor.cs` — Composition-driven ambient motion,
  capped and throttled by the two monitors above. Should work as written.
- `Suppression/FocusAssistController.cs` — reading/writing Focus Assist
  state. **Read is stubbed, write is deliberately unimplemented.**
  Windows has no public API for this; the known lever is an undocumented
  registry blob, and writing it blind risks corrupting the user's
  quiet-hours profile. Needs the blob format confirmed by diffing the
  registry before/after toggling Focus Assist from Settings on a real
  machine before this is filled in.

## Terminal emulator

A second window (`Terminal/TerminalWindow.xaml`, opened via the "Open
Terminal" button on the main overlay) is a real terminal emulator, not a
themed command box:

- **`Terminal/Pty/`** — `ConPtyInterop.cs` P/Invokes the Windows ConPTY
  API (`CreatePseudoConsole`, Windows 10 1809+) and `PseudoConsoleSession.cs`
  spawns a real shell (`%COMSPEC%` by default) attached to it, exposing
  raw output bytes and accepting raw input bytes.
- **`Terminal/TerminalHostControl.xaml`** — hosts
  [xterm.js](https://github.com/xtermjs/xterm.js) (vendored under
  `Terminal/WebAssets/vendor/`, MIT-licensed — the same terminal emulator
  library VS Code's integrated terminal and Hyper use) inside a WebView2
  control, and bridges it to the ConPTY session over WebView2's
  postMessage channel. xterm.js owns VT/ANSI parsing and rendering;
  this class only shuttles bytes both ways.

That real shell pane sits on the left. The two features originally asked
for live in a panel on the right, deliberately separate from the shell's
own input:

- **Timed loops** — schedule a prompt to fire on an interval (`loop add
  5m check inbox`), shown as cards with a progress-bar countdown to the
  next trigger. `Terminal/Services/LoopScheduler.cs` drives every loop's
  countdown off one shared 1-second timer. **Not wired to a real Claude
  call yet** — `Terminal/Services/IPromptExecutor.cs` is the seam for
  that, currently bound to `SimulatedPromptExecutor`, which only echoes
  `[simulated] would send to Claude: ...`. Wiring a real executor (Claude
  Code CLI? Messages API? a claude.ai session?) is an open decision —
  each has different auth and account implications.
- **Accounts** — `Terminal/Services/AccountManager.cs` holds a list of
  locally-defined labels (not authenticated logins) and which one is
  "active," persisted to `%LOCALAPPDATA%\Overlight\accounts.json`. The
  window header always shows the active label; the Accounts panel lists
  all of them and switches on click. This is bookkeeping for which
  context you're conceptually working in — it does not authenticate
  against any real Claude account or switch which shell session you're
  in. If the intent is actually switching between authenticated
  claude.ai/Claude Code sessions, that needs a real design pass before
  this can back it.

Both panels are also drivable from Overlight's own command line at the
bottom of the window (`help` lists all commands) — that input is
separate from the shell pane above it, which owns keyboard focus for
whatever's actually running in it.

**Known gaps, flagged rather than silently assumed:**
- WebView2 Runtime must be present on the machine (preinstalled on
  Windows 11; redistributable on Windows 10) — not checked/installed by
  this code.
- `%COMSPEC%` (cmd.exe) is the hardcoded default shell; no picker yet
  for PowerShell/WSL/pwsh.
- Assumes UTF-8 in both directions between ConPTY and the shell —
  matches how Windows Terminal talks to ConPTY, but worth confirming
  against classic cmd.exe codepage behavior on a real machine.
- The `Microsoft.Web.WebView2` package version pinned in the `.csproj`
  hasn't been checked for compatibility with the pinned
  `Microsoft.WindowsAppSDK` version — `dotnet restore` may need it bumped.

## Building

This is a WinUI 3 (Windows App SDK) project and only builds on Windows.
This container is Linux, so the code here has been written and reviewed
but not compiled — build it on a Windows machine with the Windows App
SDK and .NET 8 SDK installed:

```
dotnet build Overlight.sln
```

## Layout

```
docs/DESIGN.md              — the design framework this app follows
src/Overlight.App/
  ReadLayer/                 — window list, notification list (read-only)
  Presentation/               — overlay surface, ambient motion, throttling
  Suppression/                 — the one allowed mutation: Focus Assist
  Terminal/
    Pty/                        — ConPTY interop, real shell process
    WebAssets/vendor/           — vendored xterm.js + fit addon (MIT)
    TerminalHostControl.xaml    — WebView2 host bridging xterm.js <-> Pty
    Services/                   — loop scheduler, account manager
```
