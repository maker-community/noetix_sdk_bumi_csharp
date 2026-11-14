# Noetix.Sdk.Bumi

Unofficial community C# binding for the official [Noetix Bumi SDK](https://github.com/Noetix-Robotics/noetix_sdk_bumi)
(`noetix_sdk_bumi`, SDK 3.1.0). **Not affiliated with or endorsed by Noetix Robotics.**

The official SDK is a closed-source C++ library (`libnoetix_sdk.a`) with C++ and Python entry points only. This repository
adds a thin flat C layer on top of it so that .NET can call it, mirroring what the official `pybind/` modules do for Python.

```text
your .NET app
   │  Noetix.Sdk.Bumi           C# classes named like the official ones (HighController, LowController, MediaController)
   │  P/Invoke (LibraryImport)
libnoetix_bumi_c.so / _low_c.so  native/  extern "C" wrappers, built here
   │
libnoetix_sdk.a + libddsc.so    official SDK, NOT included in this repository
```

## Layout

| Path | Contents |
| --- | --- |
| `native/` | C headers (`include/`), C++ implementation (`src/`), CMake build for the two native libraries |
| `src/Noetix.Sdk.Bumi/` | C# binding library (`net8.0`, `net10.0`) |
| `tests/Noetix.Sdk.Bumi.Tests/` | ABI contract tests (struct layout, enum values, header ⇄ binding ⇄ implementation parity) |
| `samples/Noetix.Sdk.Bumi.StatusMonitor/` | Read-only smoke test that prints robot state |
| `docs/` | [verification.md](docs/verification.md): what has and has not been verified |

## Build

Linux only (the official libraries are Linux `.so`/`.a`); aarch64 (the Bumi EDU compute board) and x86_64 are supported.
The official static library contains **GCC 11 LTO bytecode**, so the native layer must be built with GCC 11
(Ubuntu 22.04, as in the official README). Newer compilers fail at link time.

```bash
# 1. Get the official SDK (not redistributed here)
git clone https://github.com/Noetix-Robotics/noetix_sdk_bumi

# 2. Build the native wrapper
sudo apt-get install -y cmake g++ libeigen3-dev
cmake -S native -B native/build -DNOETIX_SDK_ROOT=$PWD/noetix_sdk_bumi
cmake --build native/build -j
#    -> native/build/libnoetix_bumi_c.so      (library + HighController + MediaController)
#    -> native/build/libnoetix_bumi_low_c.so  (library + LowController)

# 3. Build and test the C# side (the default tests do not load native code and also run on Windows)
dotnet test

# 4. Optional: native smoke tests (no robot needed; Linux, libraries on LD_LIBRARY_PATH)
NOETIX_BUMI_NATIVE_TESTS=1 LD_LIBRARY_PATH=native/build dotnet test
```

Two libraries are needed because the official `HighController` and `LowController` define the same global symbols and
cannot be linked together; the official Python bindings are likewise separate modules. Use only one of the two
controllers per process.

At run time the native libraries must be found by the loader (next to your executable or in `LD_LIBRARY_PATH`), together
with the official `libddsc.so.11` / `libddscxx.so.11` (the build adds an RPATH to the SDK checkout). The official docs
mention `libssl1.1` for the DDS client; the x86_64 build on Ubuntu 22.04 linked `libssl.so.3` instead.

The NuGet package contains only the managed binding; the native libraries are not bundled (they embed the official
SDK), so you build them yourself as above. `NoetixSdk` (version check, `SetDdsConfig`) always loads
`libnoetix_bumi_c.so`, so a LowController application needs both native libraries present.

## Usage

```csharp
using Noetix.Sdk.Bumi;

NoetixSdk.SetDdsConfig("config/dds.xml");        // before any Init(); sets CYCLONEDDS_URI for native code

var high = HighController.Instance;
// Init() blocks while no robot is reachable (observed > 60 s): call it off the UI thread, with a timeout.
if (!high.Init()) return;

high.SubscribeRobotHardwareStatus((in RobotHardwareStatus s) =>
    Console.WriteLine($"workmode={s.WorkMode} soc={s.BmsData.BatterySoc}"));

// The official SDK expects continuous publishing (its example loops at ~100 Hz); send Default when idle.
// Check workmode first; the official docs require the mode-switch flow before actions.
high.PublishCmd(0, 0, 0, ControlCmd.Swing);
```

API names follow the official SDK (`Init`, `PublishCmd`, `SubscribeRobotHardwareStatus`, `ControlCmd`, ...),
converted to C# casing. Behaviour differences are limited to:

- `PublishCmd` rejects non-finite or out-of-range (`|v| > 1`) `x`, `y`, `z` with `NoetixSdkException`.
- Subscribing again replaces the previous callback (the official subscription is registered once); `null` removes it.
- Exceptions thrown by your callbacks are reported via `NoetixSdk.CallbackError` and never cross into native threads.
- Native errors surface as `NoetixSdkException` (`StatusCode`, plus the SDK message).

## Safety

`HighController` keeps the factory gait and balance; `LowController` sends motor-level commands. Follow the official
safety manual: flat dry ground, no stairs or slopes above 20°, clear space around the robot, and use a gantry while
developing. Callbacks run on SDK threads and must return quickly.

## Status

See [docs/verification.md](docs/verification.md) for exactly what has and has not been verified. In short: the native
layer builds with GCC 11 on x86_64 and the C# ⇄ native path is tested without a robot; **nothing has been run against a
physical robot or on aarch64 yet.**

## License

[MIT](LICENSE). The official SDK is BSD-3-Clause and remains the property of its authors.
