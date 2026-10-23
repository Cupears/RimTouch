# Testing

## Automated checks

```powershell
pwsh -File Source/build.ps1 -Test
```

- Quick taps: separate begin and end frames.
- Two contacts: stable IDs and movement deltas.
- Long render stalls: latest move retained.
- Contact ID reuse: next touch retained.
- Remote pointer: press, drag, release, quick tap, mode switch, and focus loss.
- Focus loss: canceled contacts without a release click.
- Display scaling: client coordinates mapped to render coordinates.
- Native data layout: Windows TOUCHINPUT size.
- Game compatibility: all Harmony patch classes applied to local 1.7 files.

Reference build: RimWorld 1.7.4985 rev1269, Windows x64, .NET 10, Harmony 1.7 package.
Automated checks cover code and patch attachment; physical device checks remain separate.

## Device checklist

Start with Harmony and RimTouch only, then add the normal mod list.
For mouse-only RDP or Steam Link, enable Remote pointer mode; check one-finger controls and wheel zoom.

- [ ] Main menu buttons, nested menus, text fields, sliders, and scrolling.
- [ ] Map tap, double tap, pan, hold menu, and hold-drag selection.
- [ ] Two-finger pan, pinch zoom, and tool cancellation.
- [ ] Building placement and drafting targets.
- [ ] World map pan, zoom, and selection.
- [ ] UI scales 1.0, 1.5, and 2.0.
- [ ] Window resize and a second monitor with a different display scale.
- [ ] Alt-Tab during a gesture; no leftover click or movement.
- [ ] Physical mouse after touch; no blocked mouse input.
- [ ] Touch mode disabled; normal Windows mouse behavior restored.
- [ ] Camera and UI mods added one at a time.

## Bug report fields

- Device model and Windows version.
- Direct play or remote/streaming connection.
- RimWorld build and beta version.
- Input status from mod settings.
- Gesture, expected result, actual result, and steps.
- Active camera/UI mods.
- Player.log with personal paths removed if needed.


## Touch-only toolbar checks (beta 4)
- Start a map with Remote pointer mode enabled. No keyboard connected.
- Pan: tap Pan, drag slowly, pause, keep dragging, then release. No selection box or release selection.
- Select: tap Select, drag around pawns, release, then tap Pan.
- Action: select a pawn, tap Action, then tap a target. Choose an order by touch.
- World map: select a caravan, tap Action, then tap a destination.
- Zoom: tap Zoom + and Zoom - on both map and world map, without wheel or pinch.
- Cancel a build tool and close a dialog using Cancel. Confirm a dialog with Accept.
- Pause and cycle speeds with the toolbar. Open and close Help.
- Press a toolbar button, slide off and release: no action and no map interaction.
- Press a toolbar button, lose focus, return: no stuck contact or action.
- Open a text field and use the Windows or streaming client on-screen keyboard.
- Test both native touch and single-pointer Steam Link / RDP. Physical device results must be recorded separately from automated checks.
