---
name: quickdock-security
description: Security auditing, path traversal prevention, Zip-Slip defenses, command injection protection, and credential handling.
---

# QuickDock Security Guidelines

## File System & Path Traversal
- Validate all file paths with canonicalization checks (`Path.GetFullPath()`) before reading, writing, or extracting.
- Zip extraction must verify that every destination path begins with the target extraction directory (Zip-Slip defense).

## Command & Process Execution
- Never execute commands through shell interpolation (`cmd /c ... + untrustedInput`).
- Use parameterized `ProcessStartInfo.ArgumentList` instead of raw string arguments where possible.
- Terminal / Developer tools must sanitize shell inputs.

## Privacy & Local-First Principles
- No telemetry, no external analytic beacons, no background pinging.
- Network tools run purely on-demand when explicitly triggered by the user.
- Local SQLite database must use parameterized SQL queries with zero raw string concatenations.

