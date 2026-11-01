---
name: quickdock-runtime-testing
description: Physical runtime testing, P/Invoke automation, and regression test suites.
---

# QuickDock Runtime Testing Guidelines

## Real Environment Execution
- Run tests on real built binaries in Release configuration (`bin/Release/.../QuickDock.App.exe`).
- Test multi-monitor interactions on all physically connected displays (`DISPLAY1`, `DISPLAY5`).
- Never claim physical verification for configurations not physically present (e.g. 3rd monitor).

## Test Verification Standards
- Maintain 240+ unit and integration tests passing with 0 failures and 0 skipped.
- Audit `firstchance.log` and Serilog output for unhandled exceptions or COM failures.

