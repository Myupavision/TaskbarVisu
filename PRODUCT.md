# Product

<!-- impeccable:product-schema 1 -->

## Users

Two audiences, equally important:

- **Desktop customizers**: people who style their Windows 11 setup and want something that moves with their music, without a window in the way.
- **Musicians, producers, DJs and sound engineers**: people who want real meters (loudness, VU, stereo image, tuner, key) visible at all times while they work or listen.

## Product Purpose

TaskbarVisu draws small audio visualizers and meters directly into the empty space of the Windows 11 taskbar, on each side of the icons. Success: the user forgets it is an app; it is simply part of the taskbar, always there, always right.

## Positioning

- **It lives inside the taskbar.** No window, no frame, click-through, in the free space next to the icons, sized to the real taskbar.
- **The meters are real instruments.** BS.1770 loudness (LUFS), VU ballistics, stereo imager and phase correlation, tuner and key detection checked against a permanent test bench (`tests/key-bench.csv`).

## Operating Context

Runs in the background from the tray, started with Windows. Captures the whole PC, one output, one app (process loopback) or an input. Settings window styled like Windows 11 Settings. UI in English, French, Spanish and German.

## Capabilities and Constraints

- Windows 11 x64, horizontal taskbar only. C# .NET 10, WPF, SkiaSharp, NAudio.
- 15 modules: levels + LUFS, spectrogram, spectrum, VU meter, stereo imager, tuner / key, waveform, loudness history, piano roll, XY oscilloscope, raw data, waterfall, hyperspace, melody, ring.
- Measured footprint: about 4 % of one core at 60 fps, about 0 % in silence (see the README's numbers; re-measure before changing them).
- Per-user installer with in-app updates, or a portable zip. Not code-signed yet: SmartScreen shows "Unknown publisher".
- Free and open source, GPL-3.0. No account.
- GitHub home moved to the `Myupavision` account (2026-11-06); the repository is not yet published there.

## Brand Commitments

- Name: TaskbarVisu.
- Binding look: pixel / dot-matrix / CRT phosphor / y2k test equipment (VFD, LCD, Nixie). No glossy look.
- The settings GUI must stay simple.
- Inspired by MiniMeters; not affiliated with it.

## Evidence on Hand

- Real renders from the app itself: `docs/hero.gif` (before/after taskbar), `docs/modules/*.gif` (one per module), `docs/appearance.png` / `docs/appearance.fr.png` (settings window), `docs/welcome.png`, and anything newly rendered with the app's `--dump` mode. The hero's centre icons are neutral placeholders, not real Windows icons.
- Key-detection test bench: `tests/key-bench.csv`.
- No testimonials, reviews, user counts or press. Never invent them.

## Product Principles

1. Show, don't tell: the visuals are the argument.
2. Truthful meters: every number on screen is measured, never decorative.
3. Invisible until wanted: no window, click-through, quiet in silence.
4. Simple first, depth on demand: essentials visible, advanced options folded.
