# Verification status

Verified against official SDK 3.1.0 (`noetix_sdk_bumi`, shallow clone, November 2026).

## Verified

| Check | Result |
| --- | --- |
| Native wrapper compiles and links against the official `libnoetix_sdk.a` + CycloneDDS | Yes, GCC 11.4 / Ubuntu 22.04 / x86_64, no warnings |
| Exported symbols | `libnoetix_bumi_c.so`: 51 `noetix_*` functions; `libnoetix_bumi_low_c.so`: 7 |
| Same wrapper with GCC 15 | **Fails**: the official static library holds GCC 11 LTO bytecode |
| High and Low in one library | **Fails**: both define `noetix::robot_hardware_status_callback`; hence two libraries |
| Struct sizes/offsets, enum values (C header ⇄ C# ⇄ official `common.h`) | Unit tests |
| Every function in the headers has a P/Invoke and a native definition | Unit test |
| .NET loads both libraries and calls into them on Linux (version, argument validation, error messages, `setenv` of `CYCLONEDDS_URI`) | 7 smoke tests, Linux container |
| CycloneDDS reads the config passed through `NoetixSdk.SetDdsConfig` | Yes (the parser printed a warning for the file) |

## Observed

- In an isolated loopback-only network namespace (`unshare -Urn`) with the community MuJoCo simulator
  (`bumi_mujoco`), the C# binding receives 21-motor status at 500 Hz **only after** the simulator's status
  writer declares a 2 ms DDS deadline. With the unmodified writer, the official SDK readers (C++ and C#)
  receive zero samples while a plain Python reader still works.
- The HighController command path was exercised against the simulator: `Switch` then `Walk` moved `workmode`
  1 → 2. Earlier follow-distance results were calculated from commanded motion rather than the MuJoCo pose,
  and the displayed target had a separate timeline. **Those results do not verify following or clearance.**
  The revised follow prototype is offline-only and uses synthetic observations from the actual simulated pose;
  it does not test the SDK command path or represent camera perception or real-world safety.
- In one 15-second x86_64 MuJoCo scripted approach/dropout scenario, the revised prototype measured a minimum
  base-to-marker center distance of 1.816 m, returned zero commands for 100 simulated dropout frames, and
  moved the base 0.008 m during the dropout. The marker has no collision; these results are not a collision
  test or a safety distance. Headless execution exited successfully; the WSLg viewer completed the simulation
  but terminated with a native memory error during shutdown.
- `HighController.Init()` with no robot reachable does not return (still blocked after 60 s). Call it off the UI thread
  with a timeout.
- The x86_64 build on Ubuntu 22.04 links `libssl.so.3`, not `libssl1.1` as the official FAQ suggests.
- Separately from the SDK, a community Xbox receiver driver was built and tested for Linux input on one Bumi compute
  board. See [xbox-wireless-controller.md](xbox-wireless-controller.md) for the environment and limits.

## Not verified

- SDK binding behavior on a physical Bumi, including: `Init()` against a real motion controller, status callbacks, `PublishCmd`,
  audio/video streams, and what the `format` field of `AudioStream` means beyond the official example (`2`).
- aarch64 (the Jetson compute board) builds and runtime.
- Behaviour when the application stops sending commands (does the robot stop by itself?).
- Callback threading limits (the callbacks are invoked on SDK threads; this binding copies/forwards but does not
  throttle them).
- Redistribution of the built native libraries (they embed the official BSD-3-Clause static library).
- LowController at runtime. It is exposed for parity with the official SDK; a managed runtime is not suited to
  hard-real-time loops.
