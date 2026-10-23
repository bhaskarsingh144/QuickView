# Contributing to QuickView

Thank you for your interest in contributing to QuickView! This document provides guidelines for contributing to the project.

## Getting Started

### Prerequisites

- Windows 10 or Windows 11
- .NET Framework 4.5 or later (comes pre-installed with Windows)
- Basic knowledge of C# and Windows Forms

### Building from Source

1. Clone the repository:
   ```bash
   git clone https://github.com/bhaskarsingh144/QuickView.git
   cd QuickView
   ```

2. Run the build script:
   ```powershell
   .\scripts\build.ps1
   ```

3. The compiled executable will be in `bin\QuickView.exe`

4. To run standalone (no installation):
   ```batch
   .\scripts\setup-standalone.bat
   ```

5. To install system-wide:
   ```batch
   .\scripts\setup.bat
   ```
   (Run as Administrator)

## Project Structure

```
QuickView/
├── src/                       # C# source
│   ├── Program.cs             # Entry point and startup
│   ├── PanelForm.cs           # Main panel UI
│   ├── Sampler.cs             # Metrics sampling
│   ├── QuickSettingsPopup.cs  # Quick settings drawer
│   └── ...
├── tests/                     # Unit tests
├── scripts/
│   ├── build.ps1              # Compile from source
│   ├── test.ps1               # Run unit tests
│   ├── setup.bat              # System-wide install (admin)
│   └── setup-standalone.bat   # Portable build + run.bat
├── docs/                      # Manual test matrices
├── assets/                    # README screenshots
├── bin/                       # Generated output
├── LICENSE
├── CONTRIBUTING.md
└── README.md
```

## Development Guidelines

### Code Style

- Use 4 spaces for indentation
- Follow C# naming conventions (PascalCase for public members, camelCase for private)
- Keep methods focused and concise
- Add XML documentation comments for public APIs

### Testing

Automated (no NuGet):

```powershell
.\scripts\test.ps1
```

This compiles and runs unit tests under `tests/` (TextScale, SliderWheel, etc.).

Before submitting a pull request:
1. Build successfully (`.\scripts\build.ps1`)
2. Pass `.\scripts\test.ps1`
3. Run the app and verify basic functionality
4. For Text size changes, walk through [docs/TEST-TextScale.md](docs/TEST-TextScale.md)
5. Spot-check metrics and the quick settings drawer on your DPI setup

### Submitting Changes

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/your-feature`)
3. Commit your changes with descriptive messages
4. Push to your fork (`git push origin feature/your-feature`)
5. Open a pull request

### Commit Message Format

Use clear, descriptive commit messages:
- `feat: add brightness control for external monitors`
- `fix: correct GPU memory calculation`
- `docs: update README with new setup instructions`

## Known Limitations

- Temperature readings depend on Windows WMI - not all hardware exposes this data
- Brightness control requires DDC/CI compatible monitors or WMI support
- Text scale changes require system notification propagation
- Virtual desktop pinning works on Windows 10/11 only

## Reporting Issues

When reporting bugs, please include:
- Windows version
- Screen resolution and DPI
- Steps to reproduce
- Expected vs actual behavior
- Any relevant error messages from `QuickView.log` (in the app directory)

## Feature Requests

Feel free to open an issue for feature requests. Please describe:
- The use case
- Why it would be valuable
- Any implementation ideas you have

## License

By contributing, you agree that your contributions will be licensed under the same license as the project (see LICENSE file).
