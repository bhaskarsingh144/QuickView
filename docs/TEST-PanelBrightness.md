# Manual test matrix — main panel brightness strip

Thin slider under the header (above metric chips). Same drag / wheel / scroll-dim as the quick-settings drawer brightness control.

Automated: `.\scripts\test.ps1` (OpacityFade includes `PanelNormal`).

## Baseline

| Date | Build | Notes |
|------|-------|-------|
| | Panel brightness strip | |

## A. Layout

| ID | Result | Notes |
|----|--------|-------|
| A1 Under header | | Between hairline and CPU/Mem/GPU/Disk chips |
| A2 No label | | Track + thumb only |
| A3 Chip detail open | | Strip still visible (chrome) |
| A4 No controllable display | | Strip omitted; no empty gap |

## B. Drag

| ID | Result | Notes |
|----|--------|-------|
| B1 Drag track | | Brightness follows |
| B2 Drag thumb | | Same |

## C. Wheel + dim

| ID | Result | Notes |
|----|--------|-------|
| C1 Hover strip, scroll | | ±1% per notch |
| C2 While scrolling | | Panel fades nearly clear (desktop visible) |
| C3 After ~650ms idle | | Panel eases back to normal opacity |
| C4 Continuous scroll | | Stays dim until idle |
| C5 Wheel on body (not strip) | | Scrolls content / no brightness change |

## D. Regression

| ID | Result | Notes |
|----|--------|-------|
| D1 Drawer brightness | | Still works; wheel dim on drawer |
| D2 Chevron / chips / Wi-Fi | | Clicks unchanged |
