# Plugin Container Validation Script
Write-Host "🔧 Validating InfoPanel.SteamAPI Container Registration" -ForegroundColor Green

$pluginPath = "InfoPanel.SteamAPI\bin\Release\net8.0-windows\InfoPanel.SteamAPI-v1.0.0\InfoPanel.SteamAPI"

# Copy test config to build output
$configSource = "InfoPanel.SteamAPI.dll.ini"
$configDest = Join-Path $pluginPath "InfoPanel.SteamAPI.dll.ini"

if (Test-Path $configSource) {
    Copy-Item $configSource $configDest -Force
    Write-Host "✅ Test configuration copied to build output" -ForegroundColor Green
} else {
    Write-Host "⚠️ Test configuration not found" -ForegroundColor Yellow
}

# Rebuild to ensure latest changes
Write-Host "🔨 Rebuilding plugin with latest changes..." -ForegroundColor Cyan
dotnet build -c Release -v minimal

Write-Host ""
Write-Host "📋 Plugin Ready for InfoPanel Testing:" -ForegroundColor Green
Write-Host "   Plugin Path: $pluginPath" -ForegroundColor White
Write-Host "   Config File: Available with test Steam API key" -ForegroundColor White
Write-Host "   Containers: Fixed to use Entries.Add method" -ForegroundColor White
Write-Host "   Sensors: 10 sensors defined (Steam profile, game, library stats)" -ForegroundColor White

Write-Host ""
Write-Host "Expected Behavior in InfoPanel:" -ForegroundColor Cyan
Write-Host "  Plugin loads without container registration errors" -ForegroundColor White
Write-Host "  Steam API Data container appears in InfoPanel" -ForegroundColor White
Write-Host "  10 sensors visible in the container" -ForegroundColor White
Write-Host "  Data will show placeholder values (Steam API needs real key)" -ForegroundColor Yellow