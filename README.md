# Noetix.Sdk.Bumi

Unofficial community C# binding for the official [Noetix Bumi SDK](https://github.com/Noetix-Robotics/noetix_sdk_bumi)
(`noetix_sdk_bumi`, SDK 3.1.0). **Not affiliated with or endorsed by Noetix Robotics.**

本项目是官方 [Noetix Bumi SDK](https://github.com/Noetix-Robotics/noetix_sdk_bumi)
（`noetix_sdk_bumi`，SDK 3.1.0）的非官方社区 C# 绑定，**与松延动力无关联，也未经其认可**。

The official SDK is a closed-source C++ library (`libnoetix_sdk.a`) with C++ and Python entry points only. This repository
adds a thin flat C layer on top of it so that .NET can call it, mirroring what the official `pybind/` modules do for Python.

官方 SDK 是闭源 C++ 库（`libnoetix_sdk.a`），仅提供 C++ 和 Python 入口。本项目在其上添加一层轻量的 C 接口，
供 .NET 调用，其方式与官方 Python 使用的 `pybind/` 模块类似。下图中的官方 SDK 不包含在本仓库中。

```text
your .NET app
   │  Noetix.Sdk.Bumi           C# classes named like the official ones (HighController, LowController, MediaController)
   │  P/Invoke (LibraryImport)
libnoetix_bumi_c.so / _low_c.so  native/  extern "C" wrappers, built here
   │
libnoetix_sdk.a + libddsc.so    official SDK, NOT included in this repository
```

## Layout

**项目结构**

| 路径 / Path | 内容 / Contents |
| --- | --- |
| `native/` | C 头文件（`include/`）、C++ 实现（`src/`）及两个原生库的 CMake 构建 / C headers, C++ implementation and CMake builds for both native libraries |
| `src/Noetix.Sdk.Bumi/` | C# 绑定库 / C# binding library (`net8.0`, `net10.0`) |
| `tests/Noetix.Sdk.Bumi.Tests/` | ABI 契约测试：结构体布局、枚举值和头文件 ⇄ 绑定 ⇄ 实现的一致性 / ABI contract tests: struct layouts, enum values and header ⇄ binding ⇄ implementation parity |
| `samples/Noetix.Sdk.Bumi.StatusMonitor/` | 输出机器人状态的只读冒烟测试 / Read-only smoke test that prints robot state |
| `samples/Noetix.Sdk.Bumi.FollowDemo/` | [仅离线仿真的跟随原型](samples/Noetix.Sdk.Bumi.FollowDemo/README.md) / [Offline-only follow simulation prototype](samples/Noetix.Sdk.Bumi.FollowDemo/README.md) |
| `samples/Noetix.Sdk.Bumi.MotionTest/` | [高层动作实测（会动机器人）](samples/Noetix.Sdk.Bumi.MotionTest/README.md) / [High-level action test that moves the robot](samples/Noetix.Sdk.Bumi.MotionTest/README.md) |
| `samples/Noetix.Sdk.Bumi.MediaTest/` | [麦克风/扬声器/摄像头实测](samples/Noetix.Sdk.Bumi.MediaTest/README.md) / [Microphone, speaker and camera test](samples/Noetix.Sdk.Bumi.MediaTest/README.md) |
| `docs/` | [edu-pro-research.md](docs/edu-pro-research.md)：EDU-Pro 调研 / EDU-Pro research；[compute-board-services.md](docs/compute-board-services.md)：算力板服务和二次开发 / compute-board services and custom development；[motion-test.md](docs/motion-test.md)：真机高层动作（挥手）实测与复现 / on-robot high-level action test and reproduction；[voice-assistant.md](docs/voice-assistant.md)：自建语音助手设计指南（含隐私限制）/ building your own voice assistant (with its privacy limits)；[verification.md](docs/verification.md)：SDK 验证情况 / SDK verification；[xbox-wireless-controller.md](docs/xbox-wireless-controller.md)：算力板 Xbox 接收器配置与安全 / compute-board Xbox receiver setup and safety |

## Build

**构建**

For .NET 10 installation and a manual read-only status check on the ARM64 compute board, see the
[ARM64 board test guide](docs/arm64-board-test.md).
在 ARM64 算力板上安装 .NET 10 或手动进行只读状态测试，请参阅 [ARM64 板端测试指南](docs/arm64-board-test.md)。

Linux only (the official libraries are Linux `.so`/`.a`); aarch64 (the Bumi EDU compute board) and x86_64 are supported.
The official static library contains **GCC 11 LTO bytecode**, so the native layer must be built with GCC 11
(Ubuntu 22.04, as in the official README). Newer compilers fail at link time.

原生库仅支持 Linux（官方库为 Linux `.so`/`.a`）；目标架构包括 aarch64（Bumi EDU 算力板）和 x86_64。
官方静态库包含 **GCC 11 LTO 字节码**，因此原生层必须使用 GCC 11 构建（与官方 README 一样使用 Ubuntu 22.04）；
较新编译器会在链接时失败。

```bash
# 1. Get the official SDK (not redistributed here) / 获取官方 SDK（本仓库不重新分发）
git clone https://github.com/Noetix-Robotics/noetix_sdk_bumi

# 2. Build the native wrapper / 构建原生封装层
sudo apt-get install -y cmake g++ libeigen3-dev
cmake -S native -B native/build -DNOETIX_SDK_ROOT=$PWD/noetix_sdk_bumi
cmake --build native/build -j
#    -> native/build/libnoetix_bumi_c.so      (library + HighController + MediaController)
#    -> native/build/libnoetix_bumi_low_c.so  (library + LowController)

# 3. Build and test C# (default tests do not load native code and also run on Windows)
#    构建并测试 C#（默认测试不加载原生代码，也可在 Windows 上运行）
dotnet test

# 4. Optional: native smoke tests (no robot needed; Linux, libraries on LD_LIBRARY_PATH)
#    可选：原生冒烟测试（无需机器人；Linux，库需位于 LD_LIBRARY_PATH）
NOETIX_BUMI_NATIVE_TESTS=1 LD_LIBRARY_PATH=native/build dotnet test
```

Two libraries are needed because the official `HighController` and `LowController` define the same global symbols and
cannot be linked together; the official Python bindings are likewise separate modules. Use only one of the two
controllers per process.

需要两个库是因为官方 `HighController` 和 `LowController` 定义了相同的全局符号，无法链接到同一个库中；
官方 Python 绑定同样分为独立模块。每个进程只能使用其中一种控制器。

At run time the native libraries must be found by the loader (next to your executable or in `LD_LIBRARY_PATH`), together
with the official `libddsc.so.11` / `libddscxx.so.11` (the build adds an RPATH to the SDK checkout). The official docs
mention `libssl1.1` for the DDS client; the x86_64 build on Ubuntu 22.04 linked `libssl.so.3` instead.

运行时，加载器必须能找到原生库（位于可执行文件旁或 `LD_LIBRARY_PATH` 中），以及官方的
`libddsc.so.11` / `libddscxx.so.11`（构建会将 SDK 检出目录加入 RPATH）。官方文档提到 DDS 客户端需要
`libssl1.1`；但在 Ubuntu 22.04 上的 x86_64 构建实际链接的是 `libssl.so.3`。

The NuGet package contains only the managed binding; the native libraries are not bundled (they embed the official
SDK), so you build them yourself as above. `NoetixSdk` (version check, `SetDdsConfig`) always loads
`libnoetix_bumi_c.so`, so a LowController application needs both native libraries present.

NuGet 包仅包含托管绑定，不包含嵌入了官方 SDK 的原生库，因此需要按上面的方法自行构建。
`NoetixSdk`（版本检查、`SetDdsConfig`）始终加载 `libnoetix_bumi_c.so`，
所以使用 LowController 的应用需要同时提供两个原生库。

## Usage

**用法**

```csharp
using Noetix.Sdk.Bumi;

// Set CYCLONEDDS_URI for native code before any Init().
// 在任何 Init() 前为原生代码设置 CYCLONEDDS_URI。
NoetixSdk.SetDdsConfig("config/dds.xml");

var high = HighController.Instance;
// Init() blocks while no robot is reachable (observed > 60 s): call it off the UI thread, with a timeout.
// 无法连接机器人时 Init() 会阻塞（实测超过 60 秒）；请在非 UI 线程调用并设置超时。
if (!high.Init()) return;

high.SubscribeRobotHardwareStatus((in RobotHardwareStatus s) =>
    Console.WriteLine($"workmode={s.WorkMode} soc={s.BmsData.BatterySoc}"));

// Publishing must be continuous: the vendor examples loop forever (100 Hz Python / 500 Hz C++). Publish the
// action inside that stream and keep sending Default when idle. Going silent makes the robot ignore the action.
// Check workmode first; the official docs require the mode-switch flow before actions.
// 必须持续发布：官方示例是死循环（Python 约 100 Hz，C++ 约 500 Hz）。把动作放在这个持续的指令流里，
// 空闲时继续发 Default。中途停止发布会导致机器人忽略动作。先检查 workmode；官方文档要求执行动作前按流程切换模式。
high.PublishCmd(0, 0, 0, ControlCmd.Swing);
```

API names follow the official SDK (`Init`, `PublishCmd`, `SubscribeRobotHardwareStatus`, `ControlCmd`, ...),
converted to C# casing. Behaviour differences are limited to:

API 名称遵循官方 SDK（`Init`、`PublishCmd`、`SubscribeRobotHardwareStatus`、`ControlCmd` 等），
并转换为 C# 命名格式。行为差异仅包括：

- `PublishCmd` rejects non-finite or out-of-range (`|v| > 1`) `x`, `y`, `z` with `NoetixSdkException`.
- Subscribing again replaces the previous callback (the official subscription is registered once); `null` removes it.
- Exceptions thrown by your callbacks are reported via `NoetixSdk.CallbackError` and never cross into native threads.
- Native errors surface as `NoetixSdkException` (`StatusCode`, plus the SDK message).

- `PublishCmd` 会通过 `NoetixSdkException` 拒绝非有限值或超出范围（`|v| > 1`）的 `x`、`y`、`z`。
- 再次订阅会替换之前的回调（官方订阅只注册一次）；`null` 会移除回调。
- 回调抛出的异常通过 `NoetixSdk.CallbackError` 报告，不会跨入原生线程。
- 原生错误以 `NoetixSdkException` 显示（包含 `StatusCode` 和 SDK 消息）。

## Safety

**安全**

`HighController` keeps the factory gait and balance; `LowController` sends motor-level commands. Follow the official
safety manual: flat dry ground, no stairs or slopes above 20°, clear space around the robot, and use a gantry while
developing. Callbacks run on SDK threads and must return quickly.

`HighController` 保留原厂步态和平衡控制；`LowController` 发送电机级指令。请遵循官方安全手册：
选择平坦干燥的地面，避开楼梯和超过 20° 的坡面，清空机器人周围区域，开发时使用吊架。
回调运行在 SDK 线程上，必须尽快返回。

## Status

**状态**

See [docs/verification.md](docs/verification.md) for exactly what has and has not been verified. In short: the native
layer builds with GCC 11 on x86_64; the high native library also builds and its version functions can be called from C#
on an aarch64 compute board. **Controller initialization and robot status on a physical robot remain unverified.**

具体验证情况请参阅 [docs/verification.md](docs/verification.md)。简而言之：原生层已在 x86_64 上使用 GCC 11
构建；high 原生库也已在 aarch64 算力板上构建，并可从 C# 调用版本函数。**实体机器人上的控制器初始化和状态读取仍未验证。**

## License

**许可证**

[MIT](LICENSE). The official SDK is BSD-3-Clause and remains the property of its authors.

[MIT](LICENSE)。官方 SDK 采用 BSD-3-Clause 许可证，其所有权仍归原作者。
