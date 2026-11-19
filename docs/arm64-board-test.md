# ARM64 compute board: .NET 10 and read-only SDK test / ARM64 算力板：.NET 10 与只读 SDK 测试

[english](#english) | [中文](#中文)

## english

This procedure uses the [StatusMonitor sample](../samples/Noetix.Sdk.Bumi.StatusMonitor/Program.cs).
It initializes the high-level SDK and subscribes to hardware status; it does not publish motion commands.
Native library loading, `HighController.Init()`, and live hardware status callbacks have been verified on one Bumi
compute board. This does not verify motion control; for sending high-level actions (for example the wave preset) and
the reproduction steps, see [motion-test.md](motion-test.md).

### 1. Install .NET 10 Runtime on the board

On the Ubuntu 22.04 ARM64 compute board, check the architecture and existing runtime:

```bash
uname -m
"$HOME/.dotnet/dotnet" --list-runtimes
```

Expect `aarch64` and a `Microsoft.NETCore.App 10.0.x` entry. If the executable or the 10.0 runtime is missing, use
[Microsoft's install script](https://learn.microsoft.com/zh-cn/dotnet/core/install/linux-scripted-manual#scripted-install).
The following installs the runtime only in your home directory, without `sudo` or changes to APT sources:

```bash
(
  set -eu
  installer="$(mktemp)"
  trap 'rm -f -- "$installer"' EXIT
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$installer"
  bash "$installer" --channel 10.0 --runtime dotnet --architecture arm64 \
    --install-dir "$HOME/.dotnet" --no-path
)
"$HOME/.dotnet/dotnet" --list-runtimes
```

### 2. Locate the native library on the board

The original board build used GCC 11 and official SDK 3.1.0 at commit
`95b264598ed671a76f7959a69c21b1b99f40c0b2`. In the board shell:

```bash
root="$HOME/noetix-csharp-arm64-build-20261118"
native_dir="$root/build"
dds_dir="$root/official-sdk/dds/aarch64/lib"
file "$native_dir/libnoetix_bumi_c.so"
LD_LIBRARY_PATH="$native_dir:$dds_dir" ldd "$native_dir/libnoetix_bumi_c.so"
```

Expect an `ARM aarch64` ELF shared object and no `not found` entries. The library needs the matching CycloneDDS
libraries, including `libddsc.so.11` and `libddscxx.so.11`. The `.so` is not a standalone copy of those dependencies.
On a different board, use the matching libraries from the official SDK checkout and adjust `root`, `native_dir`,
and `dds_dir` as appropriate. For rebuilding, see [Build](../README.md#build).

The local backup is `native/build/linux-arm64/libnoetix_bumi_c.so`. Its SHA-256 matches the board build:
`47d706744cc1359dde5f1d92beddc3ad3a4cc68d9a099e11af3ae73d3e5264f9`. The backup is Git-ignored and will not
be present in a fresh checkout. Do not commit or redistribute it.

### 3. Publish and copy the sample

Run in PowerShell from the repository root on the development PC, which needs the .NET 10 SDK. Replace the SSH
destination with your compute board's address and account:

```powershell
$board = 'noetix@COMPUTE_BOARD_IP'
$sample = '.\samples\Noetix.Sdk.Bumi.StatusMonitor\Noetix.Sdk.Bumi.StatusMonitor.csproj'
$out = '.\samples\Noetix.Sdk.Bumi.StatusMonitor\bin\board-status-monitor'
dotnet publish $sample -c Release -r linux-arm64 --self-contained false -o $out
# After publish succeeds:
scp -P 2222 -r $out "${board}:~/"
```

### 4. Run the status check manually

On the compute board, in the same shell as step 2, replace the example `config` path with the absolute path to the
vendor-provided DDS configuration for your robot. Verify that file before starting. Do not change any running robot
services. The sample requests five seconds of status after SDK initialization; the outer timeout also bounds a
blocked `Init()` call.

```bash
config="$HOME/path/to/dds.xml"
if test -r "$config"; then
  LD_LIBRARY_PATH="$native_dir:$dds_dir" timeout --kill-after=5s 20s \
    "$HOME/.dotnet/dotnet" "$HOME/board-status-monitor/Noetix.Sdk.Bumi.StatusMonitor.dll" \
    "$config" 5
  printf 'Exit code: %s\n' "$?"
else
  printf 'DDS configuration is not readable: %s\n' "$config" >&2
fi
```

| Result | Meaning |
| --- | --- |
| `Native SDK version: 3.1.0` only | The C# process loaded the library and called the version function; status reception is not yet demonstrated. |
| `workmode=... soc=...` and `Received N status messages.` with `N > 0`, exit `0` | The process received hardware status callbacks. |
| `HighController.Init() returned false.`, exit `1` | The SDK reported initialization failure. |
| `Received 0 status messages.`, exit `2` | Initialization returned but no hardware status callback arrived in the sample window. |
| `DllNotFoundException` | Check the ARM64 library path and the `ldd` output in step 2. |
| Timeout, usually exit `124` (`137` if force-killed) | The process did not finish in time, possibly because `Init()` was blocked. |

## 中文

本流程使用 [StatusMonitor 示例](../samples/Noetix.Sdk.Bumi.StatusMonitor/Program.cs)。
它初始化高层 SDK 并订阅硬件状态，不发布运动指令。
原生库加载、`HighController.Init()` 以及实时硬件状态回调已在一台 Bumi 算力板上验证。这并不验证运动控制；
关于发送高层动作（例如挥手预设动作）以及复现步骤，见 [motion-test.md](motion-test.md)。

### 1. 在算力板上安装 .NET 10 运行时

在 Ubuntu 22.04 ARM64 算力板上，检查架构和现有运行时：

```bash
uname -m
"$HOME/.dotnet/dotnet" --list-runtimes
```

应看到 `aarch64` 以及一条 `Microsoft.NETCore.App 10.0.x` 记录。如果缺少该可执行文件或 10.0 运行时，请使用
[Microsoft 的安装脚本](https://learn.microsoft.com/zh-cn/dotnet/core/install/linux-scripted-manual#scripted-install)。
以下命令只把运行时安装到你的主目录，无需 `sudo`，也不改动 APT 源：

```bash
(
  set -eu
  installer="$(mktemp)"
  trap 'rm -f -- "$installer"' EXIT
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$installer"
  bash "$installer" --channel 10.0 --runtime dotnet --architecture arm64 \
    --install-dir "$HOME/.dotnet" --no-path
)
"$HOME/.dotnet/dotnet" --list-runtimes
```

### 2. 在算力板上定位原生库

最初的算力板构建使用 GCC 11 和官方 SDK 3.1.0，提交为
`95b264598ed671a76f7959a69c21b1b99f40c0b2`。在算力板的 shell 中：

```bash
root="$HOME/noetix-csharp-arm64-build-20261118"
native_dir="$root/build"
dds_dir="$root/official-sdk/dds/aarch64/lib"
file "$native_dir/libnoetix_bumi_c.so"
LD_LIBRARY_PATH="$native_dir:$dds_dir" ldd "$native_dir/libnoetix_bumi_c.so"
```

应看到 `ARM aarch64` 的 ELF 共享对象，且没有 `not found` 条目。该库需要匹配的 CycloneDDS
库，包括 `libddsc.so.11` 和 `libddscxx.so.11`。该 `.so` 并不是这些依赖的独立副本。
在其他算力板上，请使用官方 SDK 检出中匹配的库，并相应调整 `root`、`native_dir`
和 `dds_dir`。关于重新构建，见 [Build](../README.md#build)。

本地备份为 `native/build/linux-arm64/libnoetix_bumi_c.so`。其 SHA-256 与算力板上的构建一致：
`47d706744cc1359dde5f1d92beddc3ad3a4cc68d9a099e11af3ae73d3e5264f9`。该备份被 Git 忽略，全新检出的仓库中不会
存在该文件。不要提交或再分发它。

### 3. 发布并复制示例

在开发 PC 上从仓库根目录用 PowerShell 运行，开发 PC 需要 .NET 10 SDK。把 SSH
目标替换为你的算力板地址和账户：

```powershell
$board = 'noetix@COMPUTE_BOARD_IP'
$sample = '.\samples\Noetix.Sdk.Bumi.StatusMonitor\Noetix.Sdk.Bumi.StatusMonitor.csproj'
$out = '.\samples\Noetix.Sdk.Bumi.StatusMonitor\bin\board-status-monitor'
dotnet publish $sample -c Release -r linux-arm64 --self-contained false -o $out
# After publish succeeds:
scp -P 2222 -r $out "${board}:~/"
```

### 4. 手动运行状态检查

在算力板上，于步骤 2 的同一个 shell 中，把示例中的 `config` 路径替换为厂商提供的、适用于你的机器人的
DDS 配置文件的绝对路径。启动前请先确认该文件可读。不要更改任何正在运行的机器人
服务。示例在 SDK 初始化后请求五秒的状态；外层超时同时也限制被阻塞的
`Init()` 调用。

```bash
config="$HOME/path/to/dds.xml"
if test -r "$config"; then
  LD_LIBRARY_PATH="$native_dir:$dds_dir" timeout --kill-after=5s 20s \
    "$HOME/.dotnet/dotnet" "$HOME/board-status-monitor/Noetix.Sdk.Bumi.StatusMonitor.dll" \
    "$config" 5
  printf 'Exit code: %s\n' "$?"
else
  printf 'DDS configuration is not readable: %s\n' "$config" >&2
fi
```

| 结果 | 含义 |
| --- | --- |
| 仅有 `Native SDK version: 3.1.0` | C# 进程加载了库并调用了版本函数；尚未证明能够接收状态。 |
| `workmode=... soc=...` 以及 `Received N status messages.`，且 `N > 0`，退出码 `0` | 进程收到了硬件状态回调。 |
| `HighController.Init() returned false.`，退出码 `1` | SDK 报告初始化失败。 |
| `Received 0 status messages.`，退出码 `2` | 初始化返回了，但在示例窗口内没有收到任何硬件状态回调。 |
| `DllNotFoundException` | 检查 ARM64 库路径以及步骤 2 中的 `ldd` 输出。 |
| 超时，通常是退出码 `124`（被强制终止时为 `137`） | 进程未能及时结束，可能是因为 `Init()` 被阻塞。 |
