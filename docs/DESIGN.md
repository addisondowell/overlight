# AI-Native Desktop Shell — Design Framework

## 1. Vision

A retrofitted, AI-native desktop experience layered on top of a user's
existing Windows/Mac install — not a new OS, not a new device.
Process-centered UX: surface outputs, inputs, and status only as they
become relevant, rather than crowding the screen with persistent
app windows. The user can still manually find anything; UI primitives
are reduced, not features.

Non-negotiables: the user's filesystem, privacy, and performance must
remain intact. The product earns trust by touching less, not more.

## 2. Core Principle — "Represent, Don't Rebuild"

The product only reads and renders existing desktop state. It never
modifies, intercepts-and-blocks, or replaces system components.

This single constraint resolves most of the hard problems:

- No shell replacement (no explorer.exe swap, no WindowServer fight)
- No custom interception layers to build, secure, or maintain
- No rollback/uninstall risk — nothing underlying is ever changed
- Escape hatch is free: switching focus back to the real desktop
  IS the escape hatch, because the real desktop was never touched

## 3. Architecture — Three Layers

### Read Layer

Pulls existing state via sanctioned, non-destructive OS APIs:

- Window/process list (`EnumWindows`)
- Notification content (`UserNotificationListener`, reads what's
  already in Action Center — no interception required)
- Filesystem (plain reads)

Nothing underneath is modified.

### Presentation Layer

A fullscreen or overlay surface rendering a curated, process-centered
view of the read-layer state. Modeled on the same sanctioned
"presentation mode" APIs full-screen games use — an immersive surface,
not a shell replacement.

### Suppression

The one control action in the stack: toggling the OS's own Focus
Assist / Do Not Disturb state to mute banners and sound. Nothing is
lost — notifications still land in Action Center, still readable by
the read layer. Borrows an existing OS switch rather than inventing a
new one.

### Escape Hatch

Alt-Tab, dismiss overlay, or flip Focus off. Trivial and trustworthy
because there is no state to restore — nothing was changed in the
first place.

## 4. Platform Scope

Windows-first MVP. macOS lacks a cross-app notification-listener API
(locked down for privacy since early OS X), so it can only reach "hide
and count," not "hide and represent content" — a meaningfully weaker
version of the product. Revisit Mac once the Windows
read/presentation/suppression loop is proven.

## 5. Performance Doctrine

Ambient motion must ride the compositor, never an app-driven loop:

- `Windows.UI.Composition` / `DirectComposition` — declarative
  property animation (opacity, transform) only; DWM composites every
  window every frame regardless, so this adds no new work
- No timer+redraw loops — expensive, and a polling anti-pattern
- Cap ambient fps to ~15-30 — imperceptible loss, real GPU savings
- Auto-throttle via push notifications, not checks:
  `DWMWA_CLOAKED` (occlusion),
  `PowerRegisterForEffectivePowerModeNotifications`
  (battery saver / low power)

## 6. Design Test for New Features

Before adding any capability, ask:

- Does this require modifying or intercepting the underlying OS or
  app state? If yes, find the read-only equivalent or cut it.
- Does it still leave the user a free, instant way back to their
  normal desktop?
- Does it add a persistent per-frame cost, or a one-time declarative
  cost handed to the compositor?

If any answer breaks the pattern, it breaks the pitch.
