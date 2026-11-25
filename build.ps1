# Shift Unlocker Build Script
param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "dist"
)

$ErrorActionPreference = "Stop"
Write-Host "=== Building and Publishing Shift Unlocker ===" -ForegroundColor Cyan

# 1. Clean previous dist
if (Test-Path $OutputDir) {
    try {
        Get-ChildItem -Path $OutputDir | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    } catch {
        # Directory might be opened in Explorer
    }
}

# 2. Run Tests
Write-Host "--> Running Unit Tests..." -ForegroundColor Yellow
dotnet test ShiftUnlocker.sln -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Error "Tests failed!"
    exit 1
}

# 3. Publish self-contained single-file win-x64 executable
Write-Host "--> Publishing Self-Contained Single-File (win-x64)..." -ForegroundColor Yellow
dotnet publish ShiftUnlocker.App/ShiftUnlocker.App.csproj `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $OutputDir

# Rename ShiftUnlocker.App.exe to ShiftUnlocker.exe
if (Test-Path "$OutputDir\ShiftUnlocker.App.exe") {
    Move-Item -Force "$OutputDir\ShiftUnlocker.App.exe" "$OutputDir\ShiftUnlocker.exe"
}

# Copy default utilities.json and create Tools directory
New-Item -ItemType Directory -Force -Path "$OutputDir\Tools" | Out-Null
Set-Content -Path "$OutputDir\utilities.json" -Value "[]"

Write-Host "--> Publication successfully finished in '$OutputDir\'!" -ForegroundColor Green
Get-ChildItem $OutputDir
