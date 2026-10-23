# Manual test matrix — slider mouse wheel

Hover the quick-settings **brightness** or **volume** slider and scroll the mouse wheel. Each notch changes the value by **1** (brightness ±1%, volume ±1%).

Automated: `.\scripts\test.ps1` (SliderWheelTests).

## Baseline

| Date | Build | Notes |
|------|-------|-------|
| | Wheel nudge | |

## A. Automated

| ID | Result | Notes |
|----|--------|-------|
| A1 `scripts\test.ps1` | | TextScale + SliderWheel suites exit 0 |

## B. Brightness

| ID | Result | Notes |
|----|--------|-------|
| B1 Hover track, scroll up | | +1% per notch |
| B2 Scroll down | | −1% per notch |
| B3 At 0 / 100 | | Clamps; no wrap |
| B4 Hover thumb | | Same as track |
| B5 Pointer off slider | | Wheel does not change brightness |

## C. Volume

| ID | Result | Notes |
|----|--------|-------|
| C1 Hover track, scroll up | | +1% per notch |
| C2 Scroll down | | −1% per notch |
| C3 At 0% / 100% | | Clamps |
| C4 From muted, scroll up | | Unmutes when volume &gt; 0 |
| C5 Pointer off slider | | Wheel does not change volume |

## D. Regression

| ID | Result | Notes |
|----|--------|-------|
| D1 Drag brightness / volume | | Still works |
| D2 Text size slider / Apply | | Unchanged |
