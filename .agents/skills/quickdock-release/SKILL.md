---
name: quickdock-release
description: Packaging, release hardening, installer/uninstaller verification, and artifact checksums.
---

# QuickDock Release Guidelines

## Build Configuration
- All release artifacts must be built with configuration `Release` and architecture `win-x64`.
- Unpackaged Windows App SDK deployment must bundle all native dependencies (`WindowsAppSDKSelfContained=true`).
- Release binaries must compile with 0 warnings and 0 errors.

## Installation & Uninstallation Integrity
- Installer (`QuickDock-Setup.exe`) deploys application binaries to `%LOCALAPPDATA%\Programs\QuickDock`.
- Registry entries created in `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\QuickDock`.
- Uninstaller (`QuickDock-Setup.exe /uninstall /worker`) must cleanly terminate running processes, remove shortcuts, delete registry keys, and purge directory contents.
- Generate SHA256 checksums for release executables.

