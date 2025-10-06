#!/usr/bin/env pwsh
# Pre-commit script for Orleans - runs formatting, build, and test checks
# This script is intended to be called by the Git pre-commit hook

$ErrorActionPreference = "Stop"

Write-Host "Running pre-commit checks..." -ForegroundColor Cyan

# Change to repository root
$repoRoot = git rev-parse --show-toplevel
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Not in a git repository" -ForegroundColor Red
    exit 1
}
Set-Location $repoRoot

# Step 1: Verify code formatting
Write-Host "`n[1/3] Verifying code formatting..." -ForegroundColor Yellow
dotnet format --verify-no-changes
if ($LASTEXITCODE -ne 0) {
    Write-Host "Code formatting check failed. Run 'dotnet format' to fix formatting issues." -ForegroundColor Red
    exit 1
}
Write-Host "Code formatting check passed." -ForegroundColor Green

# Step 2: Build in Release configuration
Write-Host "`n[2/3] Building in Release configuration..." -ForegroundColor Yellow
dotnet build -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed." -ForegroundColor Red
    exit 1
}
Write-Host "Build succeeded." -ForegroundColor Green

# Step 3: Run tests with code coverage
Write-Host "`n[3/3] Running tests with code coverage..." -ForegroundColor Yellow
dotnet test -c Release --collect:"XPlat Code Coverage" --no-build
if ($LASTEXITCODE -ne 0) {
    Write-Host "Tests failed." -ForegroundColor Red
    exit 1
}
Write-Host "Tests passed." -ForegroundColor Green

Write-Host "`nAll pre-commit checks passed!" -ForegroundColor Green
exit 0
