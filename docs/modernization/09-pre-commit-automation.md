# Pre-Commit Automation

## Overview

This document describes the pre-commit automation system added to the Orleans project. The pre-commit hooks ensure code quality by running the same checks as CI before allowing commits.

## Components

### 1. Pre-Commit Scripts

Two platform-specific scripts that perform the actual quality checks:

- **`scripts/tools/precommit.ps1`** - PowerShell script for Windows
- **`scripts/tools/precommit.sh`** - Bash script for Linux/macOS

Both scripts perform identical checks:
1. Code formatting verification (`dotnet format --verify-no-changes`)
2. Build in Release configuration (`dotnet build -c Release`)
3. Tests with code coverage (`dotnet test -c Release --collect:"XPlat Code Coverage"`)

### 2. Git Hook

- **`.githooks/pre-commit`** - Platform-agnostic Git hook that detects the OS and calls the appropriate script

The hook automatically determines whether to use PowerShell or Bash based on the `$OSTYPE` environment variable.

### 3. Setup Scripts

Two installation scripts to configure Git to use the custom hooks:

- **`scripts/tools/setup-hooks.ps1`** - PowerShell setup for Windows
- **`scripts/tools/setup-hooks.sh`** - Bash setup for Linux/macOS

Both scripts:
- Configure Git to use `.githooks` directory (`git config core.hooksPath .githooks`)
- Make scripts executable (Unix-like systems)
- Display installation success message and usage instructions

## Installation

### Windows
```powershell
.\scripts\tools\setup-hooks.ps1
```

### Linux/macOS
```bash
bash scripts/tools/setup-hooks.sh
```

## Usage

### Automatic Execution

Once installed, the pre-commit hook runs automatically before every commit. If any check fails, the commit is blocked.

### Manual Execution

Run checks manually without committing:

**Windows:**
```powershell
.\scripts\tools\precommit.ps1
```

**Linux/macOS:**
```bash
bash scripts/tools/precommit.sh
```

### Bypass Hook

To commit without running checks (use sparingly):
```bash
git commit --no-verify
```

### Uninstall

To disable pre-commit hooks:
```bash
git config --unset core.hooksPath
```

## Design Decisions

### Cross-Platform Approach

The system uses a dispatcher pattern:
1. Git calls `.githooks/pre-commit` (bash script)
2. The hook detects the OS using `$OSTYPE`
3. Calls the appropriate platform-specific script (PowerShell or Bash)

This approach ensures:
- Native shell experience on each platform
- Proper error handling and exit codes
- Colored output for better readability
- Consistent behavior across platforms

### Why Not Use Git's Default Hooks Directory?

Git's default `.git/hooks` directory is not tracked in version control. Using a custom `.githooks` directory with `core.hooksPath` allows:
- Version control of hooks
- Easy distribution to all developers
- Consistency across team members
- No manual file copying

### Check Order

The checks are ordered by speed and fail-fast principle:
1. **Format check** - Fastest, catches common issues
2. **Build** - Medium speed, ensures compilation
3. **Tests** - Slowest, comprehensive validation

If formatting fails, the build and tests are skipped, saving time.

### Release Configuration

All checks use Release configuration to match CI behavior and catch optimization-related issues early.

## Integration with CI

The pre-commit hooks mirror the CI workflow steps, ensuring:
- Local validation before push
- Reduced CI failures
- Faster feedback for developers
- Consistent quality standards

## Troubleshooting

### Hook Not Running

1. Verify installation: `git config core.hooksPath`
2. Check file permissions: `ls -la .githooks/pre-commit`
3. Ensure scripts are executable (Unix): `chmod +x .githooks/pre-commit scripts/tools/*.sh`

### PowerShell Not Found (Windows)

Install PowerShell Core (pwsh): https://github.com/PowerShell/PowerShell

### Slow Execution

The full check suite runs build and tests, which can take several minutes on large repositories. Consider:
- Running `dotnet format` before committing to pass format check quickly
- Using `git commit --no-verify` for WIP commits (push with full checks)
- Ensuring the build is cached (subsequent runs are faster)

## Future Enhancements

Potential improvements:
- Incremental checking (only test changed code)
- Parallel test execution
- Caching mechanisms
- Configurable check levels (quick vs. full)
- Integration with Git LFS for large files
