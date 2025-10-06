#!/usr/bin/env bash
# Setup script to install Git hooks for Orleans

set -e

echo -e "\033[0;36mSetting up Git hooks for Orleans...\033[0m"

# Get repository root
REPO_ROOT=$(git rev-parse --show-toplevel)
if [ $? -ne 0 ]; then
    echo -e "\033[0;31mError: Not in a git repository\033[0m"
    exit 1
fi

# Make the pre-commit hook executable
chmod +x "$REPO_ROOT/.githooks/pre-commit"
chmod +x "$REPO_ROOT/scripts/tools/precommit.sh"

# Configure Git to use custom hooks path
echo -e "\033[0;33mConfiguring Git hooks path...\033[0m"
git config core.hooksPath .githooks

if [ $? -eq 0 ]; then
    echo -e "\033[0;32mGit hooks installed successfully!\033[0m"
    echo -e "\n\033[0;36mThe following checks will run before each commit:\033[0m"
    echo -e "  1. Code formatting verification (dotnet format --verify-no-changes)"
    echo -e "  2. Build in Release configuration (dotnet build -c Release)"
    echo -e "  3. Test execution with coverage (dotnet test -c Release --collect:'XPlat Code Coverage')"
    echo -e "\n\033[0;36mTo disable the pre-commit hook temporarily, use:\033[0m"
    echo -e "  git commit --no-verify"
    echo -e "\n\033[0;36mTo uninstall the hooks, run:\033[0m"
    echo -e "  git config --unset core.hooksPath"
else
    echo -e "\033[0;31mFailed to configure Git hooks\033[0m"
    exit 1
fi
