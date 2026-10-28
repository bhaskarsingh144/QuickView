# QuickView

**A lightweight system monitor for the right edge of your Windows desktop.**

Live CPU and memory at a glance on the resting panel — GPU and disk behind metric chips — with brightness and volume on the main chrome, plus a quick settings drawer for text scale and everyday system actions. No installer required. No telemetry. Runs locally.

![Windows 10/11](https://img.shields.io/badge/Windows-10%2F11-0078D6?style=flat-square&logo=windows&logoColor=white)
![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.5%2B-512BD4?style=flat-square)
![License: MIT](https://img.shields.io/badge/License-MIT-yellow?style=flat-square)
![Size](https://img.shields.io/badge/Deps-None-success?style=flat-square)

---

## Screenshots

> Drop your images into [`assets/`](assets/) and they will show up here.

| Resting panel | Metric detail | Quick settings |
| :---: | :---: | :---: |
| ![Resting panel](assets/screenshot-panel.png) | ![CPU / Memory / GPU / Disk views](assets/screenshot-metrics.png) | ![Quick settings drawer](assets/screenshot-settings.png) |

<!-- Optional full-width shots:
![Header with Wi-Fi and battery](assets/screenshot-header.png)
![Panel parked / edge reveal](assets/screenshot-park.png)
-->

---

## Features

### Real-time monitoring

- **Resting overview** — compact CPU + Memory graphs (title, current %, optional temp in the graph chrome); no scrollbar. Live clock on CPU; in-use / committed / cached + modules on Memory
- **CPU chip** — usage %, processor name, cores/threads, rated and live clock, per-core clocks, optional temperature, top processes
- **Memory chip** — in-use (Task Manager–aligned), committed, cached, paged/non-paged pools, module info, optional temperature, Trim, top processes
- **GPU chip** — 3D engine usage (capped at 100%), dedicated and shared adapter memory, optional temperature
- **Disk chip** — busy time, space in use, read/write rates, physical drive names/size/media type
- **60-second history graphs** on the resting page and in every metric view
- **Process lists** on CPU and Memory detail views, color-coded (system / Windows apps / installed apps)
- **Memory Trim** — shrinks background working sets without closing apps (Memory detail)

### Status header / chrome

- Wide **drawer** hit zone (control fills the hover rect → Quick settings); **network** (larger Wi-Fi + rates) sits next to **battery**
- Compact **battery** percent (semibold metric face) with charging indicator
- Click network → Windows network settings · Click battery → power options
- **Brightness** fill block with sun icon + level — drag or scroll ±1%; panel soft-dims while scrolling
- **Volume** vertical fill with speaker icon + level — drag or scroll ±1% (no dim)

### Quick settings drawer

Open from the left-edge drawer control (hover or click):

| Section | What you get |
| --- | --- |
| **Text** | Windows **Accessibility → Text size** (“Make text bigger”) — A− / A+ (5% steps), then **Apply** (same as Settings Apply / “Please wait”) |
| **Actions** | Grid: Explorer, Lock, Sleep, Restart, Shut down, Display, Sound, Mute, Relaunch (restarts QuickView) |

Restart and Shut down require a second confirm within two seconds.

### Smart shell behavior

- Stays visible beside normal windows; **parks off-screen** when a window is maximized or full-screen
- **Reveal on right-edge hover**; parks again when you leave
- **Pinned to all virtual desktops**; hidden from Task View clutter
- Survives **Show Desktop** correctly
- **Per-monitor DPI aware** — sharp text on high-DPI and mixed-DPI setups
- Auto-starts on login (optional; easy to disable)

---

## Quick start

### Standalone (no install)

```bat
scripts\setup-standalone.bat
```

Compiles to `bin\QuickView.exe`, creates `run.bat`, and launches the panel.

### System install

Right-click `scripts\setup.bat` → **Run as administrator**.

Installs to `C:\Program Files\QuickView`, adds Desktop and Start Menu shortcuts, and starts the app.

### Build only

```powershell
.\scripts\build.ps1
```

Output: `bin\QuickView.exe`

### Tests

```powershell
.\scripts\test.ps1
```

Runs unit tests (TextScale, SliderWheel, OpacityFade, DisplayScale). Manual matrices: [docs/TEST-TextScale.md](docs/TEST-TextScale.md), [docs/TEST-SliderWheel.md](docs/TEST-SliderWheel.md), [docs/TEST-PanelBrightness.md](docs/TEST-PanelBrightness.md), [docs/TEST-DisplayScale.md](docs/TEST-DisplayScale.md).

---

## Usage

| Situation | Behavior |
| --- | --- |
| Desktop / normal windows | Panel stays on the right edge |
| Maximized or full-screen | Parks off-screen; move pointer to the right edge to show |
| Virtual desktops | Same panel on every desktop |
| Show Desktop | Panel returns when the desktop is showing |

**Metric chips** — resting shows CPU + Memory only; click CPU, Memory, GPU, or Disk for a detailed view; click again to return to the resting page.

**Quick settings** — drawer control on the left edge of the panel (tile grid; hover opens instantly with a short fade-in). Esc or leave the drawer to close (brief leave grace).

---

## Requirements

- Windows 10 or Windows 11
- .NET Framework 4.5+ (already on modern Windows)
- No extra runtimes or packages

---

## Disabling auto-start

On first run QuickView adds a Startup shortcut. To turn that off, delete:

```text
%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\QuickView.lnk
```

The app will not recreate it unless you clear the preference under `%LOCALAPPDATA%\QuickView`.

---

## Uninstalling

**Installed with `setup.bat`**

```bat
C:\Program Files\QuickView\uninstall.bat
```

**Standalone**

1. Close QuickView  
2. Delete the repo folder  
3. Remove the Startup shortcut (path above)  
4. Optionally delete `%LOCALAPPDATA%\QuickView`

---

## Project structure

```text
QuickView/
├── src/              # C# sources (WinForms)
├── tests/            # Unit tests (TextScale, etc.)
├── scripts/          # build.ps1, test.ps1, setup*.bat
├── docs/             # Manual test matrices
├── assets/           # Screenshots for this README
├── bin/              # Build output (generated)
├── LICENSE           # MIT
├── CONTRIBUTING.md
└── README.md
```

---

## Contributing

Bugs, fixes, and ideas welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) for build steps and guidelines.

---

## Troubleshooting

| Issue | What to try |
| --- | --- |
| Won't start | Confirm .NET Framework 4.5+; check `quickview.log` next to the exe |
| Brightness does nothing | Needs DDC/CI or WMI; many external monitors lack software brightness |
| No temperature | Only shown when Windows publishes a real WMI reading — often blank on laptops |

---

## Privacy

Everything stays on your PC. QuickView does not collect, upload, or phone home with system data.

---

## License

[MIT](LICENSE) © 2026 Bhaskar Singh
