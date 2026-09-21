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
```
