---
name: quickdock-performance
description: Performance auditing, latency benchmarks, memory footprint tracking, and handle audits.
---

# QuickDock Performance Guidelines

## Latency Targets
- Cursor top-zone enter -> notch reaction: ≤ 100 ms.
- Expansion animation duration: ~180 ms.
- Collapse animation duration: ~160 ms.
- Idle CPU consumption: < 1.0% when minimized to idle notch.

## Memory & Handle Baselines
- Steady state private memory: ~145–160 MB.
- Steady state handles: ~1,250–1,380 handles (DirectX, DComposition, font caches, registry keys).
- Threads: ~34–44 threads (bounded by NVIDIA GPU driver, .NET CLR, and Windows threadpool).
- Never optimize by adding arbitrary polling loops or premature GC calls.

