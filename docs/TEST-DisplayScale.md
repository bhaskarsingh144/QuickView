# Manual test matrix — display Fit / TextFit

Targets the cramped-text case on **16″ ~1080p / FHD+ (16:10)** laptops while keeping **27″ 4K** looking as before.

Automated: `.\scripts\test.ps1` (DisplayScale + ChromeLayout suites).

## Baseline

| Date | Build | Notes |
|------|-------|-------|
| | fix/display-scale-16in-fhd | |

## A. 16″ FHD or FHD+ laptop

| ID | Result | Notes |
|----|--------|-------|
| A1 Resting panel | | Chrome size similar; body text not oversized |
| A2 Header rates | | Stable-width rates; drawer hit does not twitch; fits beside battery |
| A3 Brightness / volume labels | | Volume speaker above fill, digits inside; readable at 100–150% |
| A4 Quick settings Actions | | Icon and label do not overlap (“Shut down”, “Explorer”) |
| A5 Text size row | | A− / A+ / Apply not cramped |
| A6 Windows scale 125% / 150% | | Still usable; text tighter than before |

## B. Reference screen that already looked good

| ID | Result | Notes |
|----|--------|-------|
| B1 27″ 4K (or similar desktop) | | No obvious shrink vs prior build |

## C. Regression

| ID | Result | Notes |
|----|--------|-------|
| C1 DPI change / move monitor | | Panel remounts fonts; drawer recreates |
| C2 `.\scripts\test.ps1` | | Exit 0 (includes ChromeLayout header/volume budget) |
