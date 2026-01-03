# VoiceMaster Deployment Script
# Run this from: C:\Users\kruil\Documents\Projects\output\

$buildPath = ".\VoiceMaster\bin\x64\Release"
$pluginPath = "$env:APPDATA\XIVLauncher\devPlugins\VoiceMaster"

Write-Host "=== VoiceMaster Deployment ===" -ForegroundColor Cyan
Write-Host ""

# Check if build exists
if (-not (Test-Path "$buildPath\VoiceMaster.dll")) {
    Write-Host "❌ Build not found at: $buildPath" -ForegroundColor Red
    Write-Host "   Please build the solution first (F6 in Visual Studio)" -ForegroundColor Yellow
    exit 1
}

# Create plugin directory if it doesn't exist
Write-Host "Creating plugin directory..." -ForegroundColor Gray
New-Item -ItemType Directory -Force -Path $pluginPath | Out-Null

# Copy files
Write-Host "Copying files..." -ForegroundColor Gray

Copy-Item "$buildPath\VoiceMaster.dll" -Destination $pluginPath -Force
Write-Host "  ✓ VoiceMaster.dll" -ForegroundColor Green

Copy-Item "$buildPath\VoiceMaster.json" -Destination $pluginPath -Force
Write-Host "  ✓ VoiceMaster.json" -ForegroundColor Green

Copy-Item "$buildPath\bass.dll" -Destination $pluginPath -Force
Write-Host "  ✓ bass.dll" -ForegroundColor Green

Write-Host ""
Write-Host "✅ Deployment complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Plugin installed to:" -ForegroundColor Cyan
Write-Host "  $pluginPath" -ForegroundColor White
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. In FFXIV, type: /xlplugins" -ForegroundColor White
Write-Host "  2. Go to Dev Tools tab" -ForegroundColor White
Write-Host "  3. Reload or enable VoiceMaster" -ForegroundColor White
Write-Host ""
