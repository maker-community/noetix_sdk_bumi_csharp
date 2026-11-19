# AGENTS.md

Guidance for AI coding agents and contributors working in this repository.
The English half is normative; the Chinese half is provided for readability.

本文件面向在本仓库工作的 AI 编码代理与贡献者。**以英文部分为准**，中文部分仅供阅读。

[english](#english) | [中文](#中文)

## english

### What this repository is

An **unofficial** C# (.NET) binding for the official Noetix Bumi robot SDK (`noetix_sdk_bumi`, SDK 3.1.0).
It is not affiliated with Noetix Robotics. See [README.md](README.md) for the architecture.

```text
Noetix.Sdk.Bumi (C#, src/)  ->  P/Invoke  ->  libnoetix_bumi_c.so / libnoetix_bumi_low_c.so (C++, native/)  ->  official SDK
```

### Safety rules (highest priority)

This code can move a 17 kg humanoid robot.

- **Never run anything that sends motion commands** (`PublishCmd`, `LowController`, dances, standing up) unless the user
  explicitly asks for it in that conversation and confirms the robot is wearing its safety harness (安全带) with the
  surrounding area cleared.
- Default to **read-only** work on a robot: status subscriptions, listing services, reading devices.
- Do not stop, disable or restart robot services (for example `noetix-video-capture`) without telling the user first.
- Never use `LowController` for experiments; it is exported only for parity with the official SDK.
- Do not remove validation in `noetix_high_publish_cmd` (finite values within [-1, 1], known action).

### Secrets and personal data

- Never write passwords, tokens, keys, default robot credentials or personal data into code, docs, tests or commits.
  Refer to "the account in the official documentation" instead.
- Do not commit machine-specific paths, IP addresses of private networks (other than the official fixed robot addresses),
  or logs captured from a robot without reviewing them.

### Official SDK and licensing

- The official SDK (`libnoetix_sdk.a`, CycloneDDS libraries, headers) is **never committed or redistributed** from here.
  `.gitignore` excludes `*.so`, `*.so.*`, `*.a`, `third_party/` and `noetix_sdk_bumi/`; keep it that way.
- Built native libraries embed the official static library, so they are not published either.
- This repository is MIT-licensed. Keep the copyright header style used in `native/` sources.

### Naming and API conventions

- **Mirror the official SDK**: class names `HighController`, `LowController`, `MediaController`; method names are the
  official snake_case names converted to PascalCase (`publish_cmd` -> `PublishCmd`).
- Enum **numeric values must equal the official ones** (they are passed to native code). `ControlCmd` order is fixed.
- Keep official semantics: methods returning `bool` in the official API return `bool` (for example `Init()`); do not turn
  them into exceptions. Native failures elsewhere surface as `NoetixSdkException`.
- Callbacks run on SDK threads: keep them short, never let exceptions escape into native code, and never keep pointers
  passed to a callback after it returns.

### Changing the native ABI

Any new or changed function touches all of these; the contract tests fail if they drift:

1. `native/include/noetix_bumi_c.h` or `noetix_bumi_low_c.h` (and `static_assert` struct sizes)
2. the matching `native/src/*.cpp`
3. `src/Noetix.Sdk.Bumi/Native/NativeMethods.cs` (or `NativeLowMethods.cs`) and `Types/`
4. the public controller in `src/Noetix.Sdk.Bumi/Controllers/`
5. `NOETIX_BUMI_C_API_VERSION` if the change is not backwards compatible
6. `docs/verification.md` (what is and is not verified)

Constraints discovered the hard way:

- `HighController` and `LowController` **cannot be linked into the same library** (duplicate global symbols), hence two
  native libraries. A process uses only one controller.
- The official static library holds **GCC 11 LTO bytecode**: build the native layer with GCC 11 (Ubuntu 22.04).
- `Environment.SetEnvironmentVariable` does not reach native code on Linux; use `NoetixSdk.SetDdsConfig`.
- Native libraries are 64-bit only (aarch64, x86_64).

### Build and test

Run from the repository root.

```bash
dotnet test Noetix.Sdk.Bumi.slnx                      # contract tests; also works on Windows, loads no native code

# Native layer (Linux, GCC 11), needs a checkout of the official SDK
cmake -S native -B native/build -DNOETIX_SDK_ROOT=/path/to/noetix_sdk_bumi
cmake --build native/build -j

# Native smoke tests (Linux, no robot needed)
NOETIX_BUMI_NATIVE_TESTS=1 LD_LIBRARY_PATH=native/build dotnet test
```

- `TreatWarningsAsErrors` is on; keep the build warning-free.
- Tests must not require a robot. Anything that needs real hardware belongs in `samples/` or a manual checklist in
  `docs/`, never in the default test run.
- Do not claim something works on a robot or on aarch64 unless it was actually run there; record results in
  `docs/verification.md`.

### Documentation

- **Every document is bilingual (中英对照), and both halves stay in sync.** Change both when behaviour changes.
  Long documents use two halves with a navigation line:

  ```markdown
  # <title> / <the same title in the other language>

  [english](#english) | [中文](#中文)

  ## english

  ...

  ## 中文

  ...
  ```

  The original language goes first (English-first for `README.md` concepts, `docs/verification.md`,
  `docs/arm64-board-test.md`; Chinese-first for `docs/motion-test.md`, `docs/edu-pro-research.md`,
  `docs/compute-board-services.md`). Short documents may instead interleave each English paragraph with its Chinese
  counterpart, as `README.md` and the FollowDemo README do.
- Mark claims as **[official]** (stated by Noetix docs/source), **[third-party]** (community work such as
  `docs/xbox-wireless-controller.md`), or **[inference]** / **[measured]** (needs verification / observed on the robot),
  in both halves.
- Keep `README.md` and `docs/` in sync with behaviour changes (for example blocking `Init()`).

### Git conventions

- English commit messages in [Conventional Commits](https://www.conventionalcommits.org/) style
  (`feat:`, `fix:`, `docs:`, `test:`, `chore:`, optionally with a scope such as `feat(native):`); subject in the
  imperative, no trailing period, short body explaining why when useful.
- Split work into logical commits (license/config, native, C# types, controllers, tests, docs).
- Do **not** add `Co-authored-by` trailers for AI tools; commits are authored by the maintainer.
- Never rewrite published history or force-push unless the maintainer asks.
- Do not commit `bin/`, `obj/`, `native/build/` or any native binaries.

### Maintainer

Gil Zhang <gil.zhang.dev@outlook.com>

## 中文

### 本仓库是什么

官方 Noetix Bumi 机器人 SDK（`noetix_sdk_bumi`，SDK 3.1.0）的**非官方** C#（.NET）绑定。
与松延动力（Noetix Robotics）无关联。架构见 [README.md](README.md)。

```text
Noetix.Sdk.Bumi (C#，src/)  ->  P/Invoke  ->  libnoetix_bumi_c.so / libnoetix_bumi_low_c.so（C++，native/）  ->  官方 SDK
```

### 安全规则（最高优先级）

这段代码能让一台 17 kg 的人形机器人动起来。

- **除非用户在该对话中明确要求，并确认机器人已系好安全带（safety harness）且周围环境已清理，
  否则绝不运行任何会发送运动指令的东西**（`PublishCmd`、`LowController`、舞蹈、起身）。
- 默认对机器人做**只读**操作：订阅状态、列服务、读设备。
- 未经告知用户，不要停止、失能或重启机器人上的服务（例如 `noetix-video-capture`）。
- 绝不用 `LowController` 做实验；它只是为了与官方 SDK 对齐而导出。
- 不要删掉 `noetix_high_publish_cmd` 里的校验（有限值且在 [-1, 1] 内、已知 action）。

### 密钥与个人数据

- 绝不把密码、令牌、密钥、机器人默认凭据或个人数据写进代码、文档、测试或提交。
  改为引用"官方文档中提到的账号"。
- 不要提交本机特有的路径、私有网段 IP（官方固定的机器人地址除外），或未经检查的机器人日志。

### 官方 SDK 与许可

- 官方 SDK（`libnoetix_sdk.a`、CycloneDDS 库、头文件）**绝不从本仓库提交或再分发**。
  `.gitignore` 已排除 `*.so`、`*.so.*`、`*.a`、`third_party/` 和 `noetix_sdk_bumi/`，请保持这样。
- 构建出的原生库内嵌了官方静态库，因此也不发布。
- 本仓库为 MIT 许可。保持 `native/` 源码现有的版权头风格。

### 命名与 API 约定

- **与官方 SDK 保持一致**：类名 `HighController`、`LowController`、`MediaController`；方法名由官方 snake_case
  转为 PascalCase（`publish_cmd` -> `PublishCmd`）。
- 枚举**数值必须与官方一致**（会传给原生代码）。`ControlCmd` 的顺序是固定的。
- 保持官方语义：官方 API 返回 `bool` 的方法也返回 `bool`（例如 `Init()`），不要改成抛异常；
  其它原生失败以 `NoetixSdkException` 呈现。
- 回调运行在 SDK 线程上：必须短小，绝不让异常逃逸到原生代码，绝不在回调返回后继续持有传进来的指针。

### 修改原生 ABI

任何新增或改动的函数都要同步以下几处，契约测试会在它们不一致时报错：

1. `native/include/noetix_bumi_c.h` 或 `noetix_bumi_low_c.h`（以及 `static_assert` 的结构体大小）
2. 对应的 `native/src/*.cpp`
3. `src/Noetix.Sdk.Bumi/Native/NativeMethods.cs`（或 `NativeLowMethods.cs`）与 `Types/`
4. `src/Noetix.Sdk.Bumi/Controllers/` 里的公开控制器
5. 不向后兼容时更新 `NOETIX_BUMI_C_API_VERSION`
6. `docs/verification.md`（哪些已验证、哪些没有）

踩过坑才知道的约束：

- `HighController` 与 `LowController` **无法链接进同一个库**（全局符号重复），所以有两个原生库。一个进程只用其中一个控制器。
- 官方静态库里是 **GCC 11 的 LTO 字节码**：原生层必须用 GCC 11（Ubuntu 22.04）构建。
- Linux 上 `Environment.SetEnvironmentVariable` 传不到原生代码；请用 `NoetixSdk.SetDdsConfig`。
- 原生库只支持 64 位（aarch64、x86_64）。

### 构建与测试

在仓库根目录执行。

```bash
dotnet test Noetix.Sdk.Bumi.slnx                      # 契约测试；Windows 上也能跑，不加载原生代码

# 原生层（Linux，GCC 11），需要官方 SDK 的检出
cmake -S native -B native/build -DNOETIX_SDK_ROOT=/path/to/noetix_sdk_bumi
cmake --build native/build -j

# 原生冒烟测试（Linux，不需要机器人）
NOETIX_BUMI_NATIVE_TESTS=1 LD_LIBRARY_PATH=native/build dotnet test
```

- 已开启 `TreatWarningsAsErrors`，构建必须零警告。
- 测试不得依赖机器人。任何需要真实硬件的检查放到 `samples/` 或 `docs/` 的手工清单里，绝不进默认测试。
- 没有真机或 aarch64 上的实际运行，不要声称"能跑"；结果记录到 `docs/verification.md`。

### 文档

- **所有文档都是中英对照，两半必须同步**；行为变化时两边一起改。长文档用两个半区 + 导航行：

  ```markdown
  # <标题> / <另一语言的同一标题>

  [english](#english) | [中文](#中文)

  ## english

  ...

  ## 中文

  ...
  ```

  原文语言放在前面（`docs/verification.md`、`docs/arm64-board-test.md` 英文在前；`docs/motion-test.md`、
  `docs/edu-pro-research.md`、`docs/compute-board-services.md` 中文在前）。短文档也可以逐段交错对照，
  就像 `README.md` 和 FollowDemo 的 README 那样。
- 结论要标注 **[official]**（Noetix 文档/源码明确说明）、**[third-party]**（社区方案，例如
  `docs/xbox-wireless-controller.md`）、**[inference]** / **[measured]**（待验证 / 已在真机观察），两半都要标。
- `README.md` 与 `docs/` 要跟行为变化保持同步（例如 `Init()` 会阻塞）。

### Git 约定

- 提交信息用英文，遵循 [Conventional Commits](https://www.conventionalcommits.org/)（`feat:`、`fix:`、`docs:`、
  `test:`、`chore:`，可带范围如 `feat(native):`）；标题用祈使句，结尾不加句号，必要时在正文简述原因。
- 按逻辑拆分提交（许可/配置、原生、C# 类型、控制器、测试、文档）。
- **不要**给 AI 工具加 `Co-authored-by` trailer；提交作者是维护者。
- 除维护者要求外，绝不改写已发布历史或强制推送。
- 不要提交 `bin/`、`obj/`、`native/build/` 或任何原生二进制。

### 维护者

Gil Zhang <gil.zhang.dev@outlook.com>