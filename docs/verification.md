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
