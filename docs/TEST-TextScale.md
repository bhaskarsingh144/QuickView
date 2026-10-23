# Manual test matrix — Accessibility Text size

Drawer control for Windows **Settings → Accessibility → Text size**. A− / A+ in 5% steps, then **Apply** (same path as Settings Apply / “Please wait”).

Automated: `.\scripts\test.ps1` (TextScale suite).

## Baseline

| Date | Build | Notes |
|------|-------|-------|
| | Chrome header revamp | |

## A. Automated

| ID | Result | Notes |
|----|--------|-------|
| A1 `scripts\test.ps1` | | Exit 0; ClampAndSnap, A±, tile wrap |

## B. Drawer — pending (A− / A+)

| ID | Result | Notes |
|----|--------|-------|
| B1 A− | | −5%, marked dirty (*) |
| B2 A+ | | +5%, marked dirty (*) |
| B3 Clamp 100 / 225 | | |
| B4 Apply | | Commits; * clears; UI text scales after WNF |

## C. Regression

| ID | Result | Notes |
|----|--------|-------|
| C1 Main brightness / volume | | Still on panel chrome |
| C2 Mute tile | | Still in Actions grid |
