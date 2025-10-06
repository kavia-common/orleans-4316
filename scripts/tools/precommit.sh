#!/usr/bin/env bash
# Pre-commit script for Orleans - runs formatting, build, and test checks
# This script is intended to be called by the Git pre-commit hook

set -e

echo -e "\033[0;36mRunning pre-commit checks...\033[0m"

# Change to repository root
REPO_ROOT=$(git rev-parse --show-toplevel)
if [ $? -ne 0 ]; then
    echo -e "\033[0;31mError: Not in a git repository\033[0m"
    exit 1
fi
cd "$REPO_ROOT"

# Step 1: Verify code formatting
echo -e "\n\033[0;33m[1/3] Verifying code formatting...\033[0m"
dotnet format --verify-no-changes
if [ $? -ne 0 ]; then
    echo -e "\033[0;31mCode formatting check failed. Run 'dotnet format' to fix formatting issues.\033[0m"
    exit 1
fi
echo -e "\033[0;32mCode formatting check passed.\033[0m"

# Step 2: Build in Release configuration
echo -e "\n\033[0;33m[2/3] Building in Release configuration...\033[0m"
dotnet build -c Release
if [ $? -ne 0 ]; then
    echo -e "\033[0;31mBuild failed.\033[0m"
    exit 1
fi
echo -e "\033[0;32mBuild succeeded.\033[0m"

# Step 3: Run tests with code coverage
echo -e "\n\033[0;33m[3/3] Running tests with code coverage...\033[0m"
dotnet test -c Release --collect:"XPlat Code Coverage" --no-build
if [ $? -ne 0 ]; then
    echo -e "\033[0;31mTests failed.\033[0m"
    exit 1
fi
echo -e "\033[0;32mTests passed.\033[0m"

echo -e "\n\033[0;32mAll pre-commit checks passed!\033[0m"
exit 0
