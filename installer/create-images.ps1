# Create professional wizard images for Inno Setup using ImageMagick
# Requires ImageMagick to be installed
# Uses SVG rendering at high DPI for crisp, high-quality images

$InstallerDir = $PSScriptRoot
$AssetsDir = Join-Path (Split-Path $InstallerDir) "AutoDrop\Assets"
$LogoSvgPath = Join-Path $AssetsDir "logo.svg"
$LogoPngPath = Join-Path $AssetsDir "logo.png"

# Prefer SVG for better quality, fallback to PNG
if (Test-Path $LogoSvgPath) {
    $LogoPath = $LogoSvgPath
    $UseSvg = $true
    Write-Host "Using SVG logo for high-quality rendering..." -ForegroundColor Green
} else {
    $LogoPath = $LogoPngPath
    $UseSvg = $false
    Write-Host "SVG not found, using PNG logo..." -ForegroundColor Yellow
}

Write-Host "Creating installer wizard images..." -ForegroundColor Cyan

# ============================================================================
# LARGE WIZARD IMAGE (164x314 pixels) - Shows on left side of installer
# White background with logo and text
# ============================================================================
Write-Host "  Creating wizard-large.bmp (164x314)..."

# Step 1: Create white background
& magick -size 164x314 "xc:white" "$InstallerDir\bg_temp.bmp"

# Step 2: Render logo at high quality
if ($UseSvg) {
    # Render SVG at 300 DPI for crisp output, then resize with Lanczos filter
    & magick -density 300 $LogoPath -resize 120x120 -filter Lanczos -background none "$InstallerDir\logo_temp.png"
} else {
    & magick $LogoPath -resize 120x120 -filter Lanczos -background none "$InstallerDir\logo_temp.png"
}

# Step 3: Composite logo onto background (centered, shifted up)
& magick "$InstallerDir\bg_temp.bmp" "$InstallerDir\logo_temp.png" -gravity center -geometry +0-50 -composite "$InstallerDir\temp1.bmp"

# Step 4: Add "AutoDrop" title text (dark blue)
& magick "$InstallerDir\temp1.bmp" -gravity center -font "Segoe-UI-Bold" -pointsize 22 -fill "#1a5276" -annotate +0+50 "AutoDrop" "$InstallerDir\temp2.bmp"

# Step 5: Add subtitle (gray)
& magick "$InstallerDir\temp2.bmp" -gravity center -font "Segoe-UI" -pointsize 11 -fill "#666666" -annotate +0+78 "Smart File Organizer" "$InstallerDir\wizard-large.bmp"

# Cleanup temp files
Remove-Item "$InstallerDir\bg_temp.bmp", "$InstallerDir\logo_temp.png", "$InstallerDir\temp1.bmp", "$InstallerDir\temp2.bmp" -ErrorAction SilentlyContinue

# ============================================================================
# SMALL WIZARD IMAGE (55x55 pixels) - Shows in header of installer pages
# White background with logo
# ============================================================================
Write-Host "  Creating wizard-small.bmp (55x55)..."

# Create white background
& magick -size 55x55 "xc:white" "$InstallerDir\small_bg.bmp"

# Render logo at high quality
if ($UseSvg) {
    & magick -density 300 $LogoPath -resize 48x48 -filter Lanczos -background none "$InstallerDir\small_logo.png"
} else {
    & magick $LogoPath -resize 48x48 -filter Lanczos -background none "$InstallerDir\small_logo.png"
}

# Composite
& magick "$InstallerDir\small_bg.bmp" "$InstallerDir\small_logo.png" -gravity center -composite "$InstallerDir\wizard-small.bmp"

# Cleanup
Remove-Item "$InstallerDir\small_bg.bmp", "$InstallerDir\small_logo.png" -ErrorAction SilentlyContinue

# ============================================================================
# Verify images
# ============================================================================
Write-Host ""
Write-Host "Verifying images:" -ForegroundColor Yellow
& magick identify "$InstallerDir\wizard-large.bmp"
& magick identify "$InstallerDir\wizard-small.bmp"

Write-Host ""
Write-Host "Wizard images created successfully!" -ForegroundColor Green
