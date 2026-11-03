# Manual test matrix — main panel brightness + volume chrome

HyperOS fill blocks on the main panel: horizontal brightness under the header; vertical volume on the right spanning header + brightness.

Automated: `.\scripts\test.ps1` (SliderWheel, OpacityFade, ChromeLayout).

## Baseline

| Date | Build | Notes |
|------|-------|-------|
| | compact resting / header polish | |

## A. Layout

| ID | Result | Notes |
|----|--------|-------|
| A1 Brightness under header | | Between hairline and metric chips |
| A2 Brightness style (1 usable) | | Same as before: full `BrightBlockDip` fill, sun + level, **no** monitor name |
| A3 Volume column | | Speaker above shortened fill; level digits inside fill; drag maps to fill only |
| A4 Drawer control | | Full leftover hit left of network; 2×2 tile glyph; reserve ≥ header row |
| A5 Compact Wi-Fi / battery | | Stable-width rates + fixed battery slot; drawer hit does not twitch |
| A6 No controllable display | | Brightness omitted; volume still present (header height) |
| A7 Chip detail open | | Chrome still visible |
| A8 Resting body | | CPU + Memory only (GPU/Disk via chips) |
| A9 Two plugged, one usable | | Still single-strip look (A2); no names |
| A10 Two usable | | Two thin fills in same total height; each shows name + %; volume height unchanged |

## B. Brightness drag / wheel

| ID | Result | Notes |
|----|--------|-------|
| B1 Drag track | | Brightness follows (single strip) |
| B2 Hover + scroll | | ±1% per notch |
| B3 While scrolling | | Panel soft-dims PanelNormal → Preview (~0.64); chrome stays readable |
| B4 After ~650ms idle | | Panel eases back to **PanelNormal** (1.0, fully opaque) |
| B5 Dual: drag each row | | Only that monitor changes |
| B6 Dual: wheel per row | | Cursor over row A/B nudges that display; soft-dim still applies |

## C. Volume drag / wheel

| ID | Result | Notes |
|----|--------|-------|
| C1 Drag fill | | Volume follows (top = loud, bottom = quiet); badge above is not the drag range |
| C2 Hover + scroll | | ±1% per notch on column |
| C3 No dim | | Panel opacity unchanged while adjusting volume |
| C4 Unmute on raise | | Raising volume from muted clears mute |

## D. Regression

| ID | Result | Notes |
|----|--------|-------|
| D1 Drawer | | Actions grid only for brightness/volume (no brightness/volume sliders in drawer) |
| D2 Mute tile | | Still toggles mute from Actions grid |
| D3 Drawer / chips / Wi-Fi | | Clicks and hover-open unchanged |
