# Manual test matrix — Accessibility Text size

Covers Quick settings controls that drive **Settings → Accessibility → Text size** (“Make text bigger”).

Automated math cases: `.\scripts\test.ps1`

Ship only when every row is Pass or accepted Skip.

## Baseline

| Date | Build | Notes |
|------|-------|-------|
| | Text scale 5% + slider | |

## A. Automated

| ID | Result | Notes |
|----|--------|-------|
| A1 `scripts\test.ps1` | | Exit 0; ClampAndSnap, slider map, A±, tile wrap |

## B. Drawer — pending (A− / A+ / slider)

| ID | Result | Notes |
|----|--------|-------|
| B1 Open drawer | | Text size label matches current Accessibility %; Apply disabled |
| B2 A+ / A− | | Pending % changes by 5; `*` shown; Apply enables; browser text **not** yet changed |
| B3 Slider drag | | Pending % / thumb move; still no system change until Apply |
| B4 A+ at 225 / A− at 100 | | Clamped |

## C. Apply

| ID | Result | Notes |
|----|--------|-------|
| C1 Apply with dirty pending | | “Please wait” (or equivalent shell refresh); browser / apps redraw |
| C2 Apply when not dirty | | No-op; button stays disabled/muted |
| C3 After Apply | | `*` clears; Apply disabled; label matches Accessibility |

## D. Actions grid

| ID | Result | Notes |
|----|--------|-------|
| D1 No Text tile | | Grid has 8 actions; text only via dedicated row |

## E. Regression

| ID | Result | Notes |
|----|--------|-------|
| E1 Brightness slider | | Still drags |
| E2 Volume slider | | Still drags |
| E3 Mute / tiles | | Unchanged |

## F. Windows Settings cross-check

| ID | Result | Notes |
|----|--------|-------|
| F1 Search “Make text bigger” | | Opens Accessibility → Text size |
| F2 Apply in QuickView | | Settings page reflects new % after refresh/reopen |
| F3 Apply in Settings | | QuickView label matches after reopen drawer / next poll |
