# Build QuickView from source
# Compiles all C# files in src/ directory using .NET Framework C# compiler

$ErrorActionPreference = "Stop"

# Get script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir
$srcDir = Join-Path $rootDir "src"
$outputDir = Join-Path $rootDir "bin"

# Ensure output directory exists
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

# Find all C# source files
$csFiles = Get-ChildItem -Path $srcDir -Filter "*.cs" | ForEach-Object { $_.FullName }

if ($csFiles.Count -eq 0) {
    Write-Error "No C# source files found in $srcDir"
    exit 1
}

Write-Host "Found $($csFiles.Count) source files" -ForegroundColor Green

# .NET Framework path
$fw = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$csc = Join-Path $fw "csc.exe"

if (-not (Test-Path $csc)) {
    Write-Error "C# compiler not found at $csc. Please install .NET Framework 4.5 or later."
    exit 1
}

# Reference assemblies
$refs = @(
    "$fw\System.Windows.Forms.dll",
    "$fw\System.Drawing.dll",
    "$fw\System.Management.dll",
    "System.dll",
    "System.Core.dll"
)

# Build arguments
$exePath = Join-Path $outputDir "QuickView.exe"
$args = @(
    "/target:winexe",
    "/out:$exePath",
    "/nologo",
    "/optimize"
)

foreach ($ref in $refs) {
    $args += "/reference:$ref"
}

$args += $csFiles

Write-Host "Compiling QuickView..." -ForegroundColor Cyan
& $csc $args 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Error "Compilation failed with exit code $LASTEXITCODE"
    exit 1
}

Write-Host "Build successful: $exePath" -ForegroundColor Green
Write-Host "Run .\scripts\setup.bat to install QuickView" -ForegroundColor Yellow
