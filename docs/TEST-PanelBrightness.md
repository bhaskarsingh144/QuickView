# Manual test matrix — main panel brightness + volume chrome

HyperOS fill blocks on the main panel: horizontal brightness under the header; vertical volume on the right spanning header + brightness.

Automated: `.\scripts\test.ps1` (SliderWheel, OpacityFade).

## Baseline

| Date | Build | Notes |
|------|-------|-------|
| | Chrome header revamp | |

## A. Layout

| ID | Result | Notes |
|----|--------|-------|
| A1 Brightness under header | | Between hairline and CPU/Mem/GPU/Disk chips |
| A2 Brightness style | | Rounded fill block (~Px(34) tall), left fill by level, no thumb |
| A3 Volume column | | Vertical fill to the right of header + brightness; fill rises from bottom |
| A4 Chevron | | Hit target still ~Px(36); glyph centered |
| A5 Compact Wi-Fi / battery | | Fit beside volume; battery percent not clipped |
| A6 No controllable display | | Brightness omitted; volume still present (header height) |
| A7 Chip detail open | | Chrome still visible |

## B. Brightness drag / wheel

| ID | Result | Notes |
|----|--------|-------|
| B1 Drag track | | Brightness follows |
| B2 Hover + scroll | | ±1% per notch |
| B3 While scrolling | | Panel soft-dims to Preview floor (~0.64); chrome stays readable |
| B4 After ~650ms idle | | Panel eases back to normal opacity |

## C. Volume drag / wheel

| ID | Result | Notes |
|----|--------|-------|
| C1 Drag column | | Volume follows (top = loud, bottom = quiet) |
| C2 Hover + scroll | | ±1% per notch |
| C3 No dim | | Panel opacity unchanged while adjusting volume |
| C4 Unmute on raise | | Raising volume from muted clears mute |

## D. Regression

| ID | Result | Notes |
|----|--------|-------|
| D1 Drawer | | Text A± + Apply + Actions only (no brightness/volume sliders) |
| D2 Mute tile | | Still toggles mute from Actions grid |
| D3 Chevron / chips / Wi-Fi | | Clicks unchanged |
