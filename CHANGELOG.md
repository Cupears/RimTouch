# Changelog

## 1.7.0-beta.5 · 2026-10-23

- Collapsed the toolbar to one small Touch button by default.
- Added a smaller three-column layout, Hide button and adjustable button size.
- Folded controls after choosing Pan, Select or Action.
- Displayed the active Select or Action mode on the collapsed button.

## 1.7.0-beta.4 · 2026-10-23

- Added a touchscreen toolbar: Pan, Select, Action, Cancel, zoom, pause, speed, Accept and Help.
- Removed the Shift requirement and timed hold-to-select gesture.
- Added single-contact toolbar capture, canceled-drag checks and map input protection.
- Added on-screen control help and remote text-entry notes.

## 1.7.0-beta.3 · 2026-10-23

- Remote pointer: plain dragging pans; Shift + drag selects a group.
- Removed the hold-to-select timer from remote pointer gestures.
- Reduced remote map drag threshold to 8 pixels.
- Prevented completed drags from becoming release taps.
- Allowed slow world-map drags to pan.

## 1.7.0-beta.2 · 2026-10-23

- Fixed main-menu errors during touch cleanup and window scrolling.
- Guarded map selection access before the play interface is ready.
- Added menu and startup regression checks.

## 1.7.0-beta.1 · 2026-10-22

### Added
- Native Windows touch capture inside the game window.
- Remote pointer mode for mouse-only RDP and Steam Link.
- Touch movement deltas and quick-tap buffering.
- Focus-loss cancellation and safe contact ID reuse.
- Scroll-view touch hook and input status display.
- .NET 10 build, local checks, and beta ZIP packaging.

### Fixed
- Legacy touch input returning no contacts on 1.7.
- Unsupported three-argument raycast call.
- Inspect-panel detection for buildings, items, and zones.
- Duplicate UI click handling during native touch.
- Zoom range restoration after disabling extended zoom.
- Startup patch isolation after a missing API target.

### Changed
- RimWorld support: 1.7 only in the beta package.
- Extended zoom: disabled by default.
- Settings panel: scrollable layout and no per-frame disk writes.

### Checks
- 26 automated checks passed.
- All 28 Harmony hooks applied against RimWorld 1.7.4985.
- Physical touchscreen checklist pending.
