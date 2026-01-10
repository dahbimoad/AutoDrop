<#
.SYNOPSIS
    Build AutoDrop installer using Inno Setup (Modern Windows 11 style)
.DESCRIPTION
    Creates a professional, modern-looking installer for AutoDrop
.PARAMETER Version
    Version number (default: 1.0.0)
.PARAMETER Architecture
    Target architecture: x64, x86 (default: x64)
.EXAMPLE
    .\build-inno.ps1 -Version "1.0.0" -Architecture "x64"
#>

param(
    [string]$Version = "1.0.0",
    [ValidateSet("x64", "x86")]
    [string]$Architecture = "x64"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$InstallerDir = Join-Path $ProjectRoot "installer"
$OutputDir = Join-Path $ProjectRoot "output"
$PublishDir = Join-Path $ProjectRoot "publish\win-$Architecture"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  AutoDrop Installer Builder (Inno Setup)" -ForegroundColor Cyan
Write-Host "  Version: $Version" -ForegroundColor Cyan
Write-Host "  Architecture: $Architecture" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Check Inno Setup
Write-Host "[1/4] Checking Inno Setup..." -ForegroundColor Yellow

$InnoPath = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $InnoPath)) {
    $InnoPath = "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
}
if (-not (Test-Path $InnoPath)) {
    $InnoPath = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
}
if (-not (Test-Path $InnoPath)) {
    Write-Host "  Inno Setup not found. Installing via winget..." -ForegroundColor Yellow
    winget install --id JRSoftware.InnoSetup -e --accept-source-agreements --accept-package-agreements
    Start-Sleep -Seconds 2
    $InnoPath = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    if (-not (Test-Path $InnoPath)) {
        $InnoPath = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    }
    if (-not (Test-Path $InnoPath)) {
        throw "Failed to install Inno Setup. Please install manually from https://jrsoftware.org/isdl.php"
    }
}
Write-Host "  Inno Setup found: $InnoPath" -ForegroundColor Green

# Step 2: Publish the application
Write-Host ""
Write-Host "[2/4] Publishing AutoDrop (self-contained, single-file)..." -ForegroundColor Yellow

$ProjectPath = Join-Path $ProjectRoot "AutoDrop\AutoDrop.csproj"
$RuntimeId = "win-$Architecture"

dotnet publish $ProjectPath `
    --configuration Release `
    --runtime $RuntimeId `
    --self-contained true `
    --output $PublishDir `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:Version=$Version

if ($LASTEXITCODE -ne 0) { throw "Publish failed" }

$ExePath = Join-Path $PublishDir "AutoDrop.exe"
$ExeSize = [math]::Round((Get-Item $ExePath).Length / 1MB, 2)
Write-Host "  Published to: $PublishDir" -ForegroundColor Green
Write-Host "  AutoDrop.exe size: $ExeSize MB" -ForegroundColor Green

# Step 3: Build installer
Write-Host ""
Write-Host "[3/3] Building installer with Inno Setup..." -ForegroundColor Yellow

# Create output directory
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

$IssFile = Join-Path $InstallerDir "AutoDrop.iss"
$env:APP_VERSION = $Version

& $InnoPath /DAppVersion=$Version $IssFile

if ($LASTEXITCODE -ne 0) { throw "Inno Setup build failed" }

# Results
$InstallerPath = Join-Path $OutputDir "AutoDrop-$Version-win-$Architecture-setup.exe"
if (Test-Path $InstallerPath) {
    $InstallerSize = [math]::Round((Get-Item $InstallerPath).Length / 1MB, 2)
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  Installer Created Successfully!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Output: $InstallerPath" -ForegroundColor White
    Write-Host "  Size: $InstallerSize MB" -ForegroundColor White
    Write-Host ""
} else {
    throw "Installer not found at expected path"
}
