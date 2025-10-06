#!/usr/bin/env pwsh
# Setup script to install Git hooks for Orleans

$ErrorActionPreference = "Stop"

Write-Host "Setting up Git hooks for Orleans..." -ForegroundColor Cyan

# Get repository root
$repoRoot = git rev-parse --show-toplevel
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Not in a git repository" -ForegroundColor Red
    exit 1
}

# Configure Git to use custom hooks path
Write-Host "Configuring Git hooks path..." -ForegroundColor Yellow
git config core.hooksPath .githooks

if ($LASTEXITCODE -eq 0) {
    Write-Host "Git hooks installed successfully!" -ForegroundColor Green
    Write-Host "`nThe following checks will run before each commit:" -ForegroundColor Cyan
    Write-Host "  1. Code formatting verification (dotnet format --verify-no-changes)" -ForegroundColor White
    Write-Host "  2. Build in Release configuration (dotnet build -c Release)" -ForegroundColor White
    Write-Host "  3. Test execution with coverage (dotnet test -c Release --collect:'XPlat Code Coverage')" -ForegroundColor White
    Write-Host "`nTo disable the pre-commit hook temporarily, use:" -ForegroundColor Cyan
    Write-Host "  git commit --no-verify" -ForegroundColor White
    Write-Host "`nTo uninstall the hooks, run:" -ForegroundColor Cyan
    Write-Host "  git config --unset core.hooksPath" -ForegroundColor White
} else {
    Write-Host "Failed to configure Git hooks" -ForegroundColor Red
    exit 1
}
