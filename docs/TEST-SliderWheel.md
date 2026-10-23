# Manual test matrix — slider mouse wheel

Hover the main-panel **brightness** strip or **volume** column and scroll the mouse wheel. Each notch changes the value by **1** (brightness ±1%, volume ±1%).

Automated: `.\scripts\test.ps1` (SliderWheel suite).

## Baseline

| Date | Build | Notes |
|------|-------|-------|
| | Chrome header revamp | |

## B. Brightness (main panel)

| ID | Result | Notes |
|----|--------|-------|
| B1 Hover strip, scroll up | | +1% |
| B2 Hover strip, scroll down | | −1% |
| B3 Clamp at 0 / 100 | | |
| B4 While scrolling | | Panel soft-dims |
| B5 Pointer off strip | | Wheel does not change brightness |

## C. Volume (main panel)

| ID | Result | Notes |
|----|--------|-------|
| C1 Hover column, scroll up | | +1% |
| C2 Hover column, scroll down | | −1% |
| C3 Clamp at 0 / 100 | | |
| C4 No dim | | Opacity unchanged |
| C5 Pointer off column | | Wheel does not change volume |

## D. Regression

| ID | Result | Notes |
|----|--------|-------|
| D1 Drawer | | No brightness/volume sliders |
| D2 Text A± / Apply | | Unchanged |
