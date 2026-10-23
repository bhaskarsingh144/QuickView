# Run QuickView unit tests (no NuGet). Compiles TextScale + TextScaleTests and executes.
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir
$srcDir = Join-Path $rootDir "src"
$testsDir = Join-Path $rootDir "tests"
$outputDir = Join-Path $rootDir "bin"
$exePath = Join-Path $outputDir "TextScaleTests.exe"

if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

$fw = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$csc = Join-Path $fw "csc.exe"
if (-not (Test-Path $csc)) {
    Write-Error "C# compiler not found at $csc"
    exit 1
}

$sources = @(
    (Join-Path $srcDir "TextScale.cs"),
    (Join-Path $testsDir "TextScaleTests.cs")
)

foreach ($f in $sources) {
    if (-not (Test-Path $f)) {
        Write-Error "Missing source: $f"
        exit 1
    }
}

Write-Host "Compiling TextScaleTests..." -ForegroundColor Cyan
& $csc /nologo /optimize /target:exe /out:$exePath $sources
if ($LASTEXITCODE -ne 0) {
    Write-Error "Test compile failed"
    exit 1
}

Write-Host "Running $exePath" -ForegroundColor Cyan
& $exePath
$code = $LASTEXITCODE
if ($code -eq 0) {
    Write-Host "All tests passed." -ForegroundColor Green
} else {
    Write-Host "Tests failed (exit $code)." -ForegroundColor Red
}
exit $code
