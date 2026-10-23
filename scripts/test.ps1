# Run QuickView unit tests (no NuGet).
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir
$srcDir = Join-Path $rootDir "src"
$testsDir = Join-Path $rootDir "tests"
$outputDir = Join-Path $rootDir "bin"

if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

$fw = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$csc = Join-Path $fw "csc.exe"
if (-not (Test-Path $csc)) {
    Write-Error "C# compiler not found at $csc"
    exit 1
}

function Run-Suite([string]$name, [string[]]$sources, [string]$exeName) {
    foreach ($f in $sources) {
        if (-not (Test-Path $f)) {
            Write-Error "Missing source: $f"
            exit 1
        }
    }
    $exePath = Join-Path $outputDir $exeName
    Write-Host "Compiling $name..." -ForegroundColor Cyan
    & $csc /nologo /optimize /target:exe /out:$exePath $sources
    if ($LASTEXITCODE -ne 0) {
        Write-Error "$name compile failed"
        exit 1
    }
    Write-Host "Running $exePath" -ForegroundColor Cyan
    & $exePath
    if ($LASTEXITCODE -ne 0) {
        Write-Host "$name failed (exit $LASTEXITCODE)." -ForegroundColor Red
        exit $LASTEXITCODE
    }
    Write-Host "$name passed." -ForegroundColor Green
}

Run-Suite "TextScaleTests" @(
    (Join-Path $srcDir "TextScale.cs"),
    (Join-Path $testsDir "TextScaleTests.cs")
) "TextScaleTests.exe"

Run-Suite "SliderWheelTests" @(
    (Join-Path $srcDir "SliderWheel.cs"),
    (Join-Path $testsDir "SliderWheelTests.cs")
) "SliderWheelTests.exe"

Write-Host "All test suites passed." -ForegroundColor Green
exit 0
