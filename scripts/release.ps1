# AutoDrop Release Script for Windows
# Usage: .\scripts\release.ps1 -Version 1.0.0

param(
    [Parameter(Mandatory=$true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"

# Validate version format
if ($Version -notmatch '^\d+\.\d+\.\d+(-[a-zA-Z0-9]+)?$') {
    Write-Error "Invalid version format. Use semantic versioning (e.g., 1.0.0, 1.0.0-beta)"
    exit 1
}

Write-Host "🚀 Releasing AutoDrop v$Version" -ForegroundColor Cyan

# Check current branch
$branch = git branch --show-current
if ($branch -ne "main") {
    Write-Warning "Not on main branch (currently on $branch)"
    $continue = Read-Host "Continue anyway? (y/n)"
    if ($continue -ne "y") {
        exit 1
    }
}

# Check for uncommitted changes
$status = git status --porcelain
if ($status) {
    Write-Error "Working directory is not clean. Commit or stash changes first."
    exit 1
}

# Pull latest
Write-Host "📥 Pulling latest changes..." -ForegroundColor Yellow
git pull origin $branch

# Create tag
Write-Host "🏷️  Creating tag v$Version..." -ForegroundColor Yellow
git tag -a "v$Version" -m "Release v$Version"

# Push tag
Write-Host "📤 Pushing tag to GitHub..." -ForegroundColor Yellow
git push origin "v$Version"

Write-Host ""
Write-Host "✅ Done! GitHub Actions will now:" -ForegroundColor Green
Write-Host "   1. Build for win-x64, win-x86, win-arm64"
Write-Host "   2. Create ZIP archives"
Write-Host "   3. Publish GitHub Release"
Write-Host ""
Write-Host "🔗 Watch progress at your GitHub repo /actions" -ForegroundColor Cyan
