# AutoDrop Local MSI Build Script
# Usage: .\scripts\build-local.ps1
# Creates MSI installer locally for testing

param(
    [string]$Version = "1.0.0",
    [string]$Architecture = "x64"
)

$ErrorActionPreference = "Stop"
$RootDir = Split-Path -Parent $PSScriptRoot
$PublishDir = Join-Path $RootDir "publish\win-$Architecture"
$InstallerDir = Join-Path $RootDir "installer"
$OutputDir = Join-Path $RootDir "output"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  AutoDrop Local MSI Builder" -ForegroundColor Cyan
Write-Host "  Version: $Version" -ForegroundColor Cyan
Write-Host "  Architecture: $Architecture" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Check if WiX is installed
Write-Host "[1/4] Checking WiX Toolset..." -ForegroundColor Yellow
$wixInstalled = $null
try {
    $wixInstalled = dotnet tool list --global | Select-String "wix"
} catch {}

if (-not $wixInstalled) {
    Write-Host "  Installing WiX Toolset v5..." -ForegroundColor Gray
    dotnet tool install --global wix --version 5.0.0
    wix extension add WixToolset.UI.wixext/5.0.0 -g
} else {
    Write-Host "  WiX Toolset already installed" -ForegroundColor Green
}

# Ensure UI extension is added
Write-Host "  Ensuring WiX UI extension..." -ForegroundColor Gray
try {
    wix extension add WixToolset.UI.wixext/5.0.0 -g 2>$null
} catch {}

# Step 2: Build and publish the application
Write-Host ""
Write-Host "[2/4] Publishing AutoDrop (self-contained, single-file)..." -ForegroundColor Yellow

# Clean previous publish
if (Test-Path $PublishDir) {
    Remove-Item -Path $PublishDir -Recurse -Force
}

$projectPath = Join-Path $RootDir "AutoDrop\AutoDrop.csproj"

dotnet publish $projectPath `
    --configuration Release `
    --runtime win-$Architecture `
    --self-contained true `
    --output $PublishDir `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:Version=$Version

if ($LASTEXITCODE -ne 0) {
    Write-Error "Publish failed!"
    exit 1
}

Write-Host "  Published to: $PublishDir" -ForegroundColor Green

# Check that AutoDrop.exe exists
$exePath = Join-Path $PublishDir "AutoDrop.exe"
if (-not (Test-Path $exePath)) {
    Write-Error "AutoDrop.exe not found at $exePath"
    exit 1
}

Write-Host "  AutoDrop.exe size: $([math]::Round((Get-Item $exePath).Length / 1MB, 2)) MB" -ForegroundColor Gray

# Step 3: Build MSI
Write-Host ""
Write-Host "[3/4] Building MSI installer..." -ForegroundColor Yellow

# Create output directory
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

$wxsFile = Join-Path $InstallerDir "Product.wxs"
$msiOutput = Join-Path $OutputDir "AutoDrop-$Version-win-$Architecture-setup.msi"

# Build MSI using WiX v5
wix build $wxsFile `
    -ext WixToolset.UI.wixext `
    -d PublishDir=$PublishDir `
    -d ProjectDir=$RootDir `
    -d Version=$Version `
    -arch $Architecture `
    -o $msiOutput

if ($LASTEXITCODE -ne 0) {
    Write-Error "MSI build failed!"
    exit 1
}

# Step 4: Done!
Write-Host ""
Write-Host "[4/4] Build complete!" -ForegroundColor Green
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  MSI Created Successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Output: $msiOutput" -ForegroundColor White
Write-Host "  Size: $([math]::Round((Get-Item $msiOutput).Length / 1MB, 2)) MB" -ForegroundColor Gray
Write-Host ""
Write-Host "  To install:" -ForegroundColor Yellow
Write-Host "  > Start-Process '$msiOutput'" -ForegroundColor Gray
Write-Host ""
Write-Host "  Or double-click the MSI file in:" -ForegroundColor Yellow
Write-Host "  > explorer.exe '$OutputDir'" -ForegroundColor Gray
Write-Host ""

# Open output folder
$openFolder = Read-Host "Open output folder? (y/n)"
if ($openFolder -eq "y") {
    explorer.exe $OutputDir
}
