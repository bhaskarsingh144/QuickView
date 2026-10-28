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
| A2 Brightness style | | Rounded fill (`BrightBlockDip`), sun + level inside, no thumb |
| A3 Volume column | | Speaker above shortened fill; level digits inside fill; drag maps to fill only |
| A4 Drawer control | | Full leftover hit left of network; 2×2 tile glyph; reserve ≥ header row |
| A5 Compact Wi-Fi / battery | | Stable-width rates + fixed battery slot; drawer hit does not twitch |
| A6 No controllable display | | Brightness omitted; volume still present (header height) |
| A7 Chip detail open | | Chrome still visible |
| A8 Resting body | | CPU + Memory only (GPU/Disk via chips) |

## B. Brightness drag / wheel

| ID | Result | Notes |
|----|--------|-------|
| B1 Drag track | | Brightness follows |
| B2 Hover + scroll | | ±1% per notch |
| B3 While scrolling | | Panel soft-dims PanelNormal → Preview (~0.64); chrome stays readable |
| B4 After ~650ms idle | | Panel eases back to **PanelNormal** (0.94), not drawer Normal |

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
| D1 Drawer | | Text A± + Apply + Actions only (no brightness/volume sliders) |
| D2 Mute tile | | Still toggles mute from Actions grid |
| D3 Drawer / chips / Wi-Fi | | Clicks and hover-open unchanged |
