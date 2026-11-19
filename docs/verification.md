# Verification status / 验证状态

[english](#english) | [中文](#中文)

## english

Verified against official SDK 3.1.0 (`noetix_sdk_bumi`, shallow clone, November 2026).

### Verified

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
| High native wrapper builds on the Bumi compute board | Yes, GCC 11.4 / Ubuntu 22.04 / aarch64; official SDK commit `95b264598ed671a76f7959a69c21b1b99f40c0b2` |
| C# loads the high native wrapper on the compute board | Yes, .NET Runtime 10.0.15 / aarch64; SDK version `3.1.0`, C API version `1` |
| C# initializes `HighController` and receives live hardware status on a physical Bumi | Yes, two read-only ARM64 [StatusMonitor](arm64-board-test.md) runs on 2026-11-18: 2,330 and 2,324 callbacks in five-second subscription windows; both exited `0` |
| C# publishes high-level actions that the physical robot executes | Yes, 2026-11-19 on the ARM64 compute board: `Swing` (2 → 8 → 2), `Shake` (2 → 9 → 2) and `Cheer` (2 → 10 → 2) all ran and returned to walking mode by themselves; smallest ankle travel was `Swing` at 0.373 rad, largest joint delta 1.971 rad (`Cheer`); steps, the 21-joint id map and full logs in [motion-test.md](motion-test.md) |
| Media path from C# on a physical Bumi | Yes, 2026-11-19: 8-channel/16 kHz microphone capture with real signal, de-sensed 640×480@30 video with faces blurred, and a published tone echoed on the playback topic; see [MediaTest](../samples/Noetix.Sdk.Bumi.MediaTest/README.md) |

### Observed

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
- The aarch64 C# version probe did not initialize a controller or DDS and did not verify robot status.
  The subsequent read-only board test did verify live status reception. CycloneDDS warned about deprecated implicit
  units in the DDS configuration, but both runs received callbacks without changing the configuration or robot services.
- On 2026-11-19, with the robot hanging on a gantry, the robot reported `workmode` 30 (`失能模式`). One
  `Swing` followed by `Default` at ~100 Hz — 1,000 `PublishCmd` calls over 12 s — completed without exception and
  status callbacks continued (6,739 messages), but no joint moved more than 0.02 rad and `workmode` stayed 30.
  Preset actions are therefore not executed while the robot is disabled. Enabling it (`START` from `workmode` 30)
  was not sent, so motion execution on hardware remains unverified.
- On 2026-11-19 (later session) the robot reported `workmode` 2 (`走路模式`). A `Switch> Swing` sequence plus
  `Default` at ~100 Hz (1,166 `PublishCmd` calls over 20 s) also changed nothing: `workmode` stayed 2 the whole time
  and no joint moved more than 0.02 rad, while 10,503 status messages arrived normally. The command path therefore
  publishes without error, but this setup's motion controller does not act on those commands; the cause has not been
  identified. `Switch` alone did not leave walking mode, so the missing piece is not a "requires ready mode" precondition.
- **Root cause of those two failures: the sample stopped publishing.** `MotionTest` sent each action and then went
  silent for up to 6 s while it waited for the mode to change. The vendor examples never stop publishing. After
  changing the sample to stream `PublishCmd` continuously (2 ms period, `Default` when idle, action samples inside the
  stream), preset actions execute on the physical robot.
- On 2026-11-19 (C# SDK, ARM64 compute board, `MotionTest ... Switch>Swing 12 500`): the robot started in `workmode` 2,
  `Switch` left it at 2, then `Swing` moved `workmode` 2 → 8 (`出厂预设动作 打招呼`) and all 21 joints moved, the arm
  most (motor 11 by 1.712 rad, 13 by 1.535, 10 by 0.927, 12 by 0.488), with 2 action commands and 2,975 `Default`
  commands published and 6,068 status messages received. **High-level motion control from the C# binding is verified
  on hardware**, including the 挥手 preset action. Command steps, expected output and the remaining test plan are in
  [motion-test.md](motion-test.md).
- On 2026-11-19 (same setup, `MotionTest ... Cheer 12 500`): `Cheer` (`出厂预设动作 欢呼`) ran `workmode` 2 → 10 → 2
  and moved all 21 joints, the largest by 1.971 rad, with 1 action command, 2,987 `Default` commands and 6,067 status
  messages. The preset finished and returned to walking mode inside the same 12 s window, which shows preset actions
  exit on their own once complete.
- On 2026-11-19 (same setup, `MotionTest ... Shake 12 500`): `Shake` (`出厂预设动作 握手`) ran `workmode` 2 → 9 → 2;
  the largest arm delta was 1.392 rad (`arm_l3`) and the largest leg delta 0.945 rad (`leg_l1`).
- With the official 21-joint id map (0–3 left arm, 4–9 left leg, 10–13 right arm, 14–19 right leg, 20 waist) the
  sample now reports the largest deviation per region. Across the three presets the ankle travel was `Swing` 0.373 rad,
  `Shake` 0.591 rad and `Cheer` ≤0.621 rad, so **`Swing` is the preset that moves the legs least**. None of them is
  arm-only: the black-box balance controller always involves the legs (hip/knee ≈ 0.9 rad).
- On the same runs, 13 of 21 motors reported `error=1`. The official fault table lists `02H`–`0EH` only, so `1` is not
  a documented fault code; its meaning is still unknown. `mean|tau|` was 0.81–0.98 N·m.
- The microphone is **not** wired to the compute board: `/dev/snd` is only held by `pulseaudio` (control nodes, no
  capture node), a local `arecord` of 2 s produced pure silence (peak 0), while `SubscribeInternalAudioCapture`
  delivered 8 channels at 16 kHz with real signal. Channels 0–3 are the head microphones, 4–5 are reserved (peak 1),
  6–7 are the speaker echo.
- The de-sensed video stream delivers raw YUYV422 640×480 at 30 fps (614,400 bytes per frame, ~15 MB/s) with faces
  blurred, as long as `noetix-video-capture` is publishing. `SubscribeInternalVideoCapture` delivered 0 frames on this
  compute-board model.
- A 1 s 440 Hz tone published with `PublishExternalAudioPlaybackStream` was echoed on `SubscribeInternalAudioPlayback`
  (50 frames back) and the microphones peaked at 12,795 during playback versus 179 while idle, so the speaker really
  produced sound. `GetVolume()` was 1 of the 0–200 range, so the test raised it to 100 and restored it afterwards.
- The voice system reported `Sleeped` (`CmdSleeped`) while idle; external playback still worked.
- Playback quality: the playback topic is specified as **2 channels**, so sending mono (which we did first) is wrong; and
  the robot speaker breaks up on low frequencies. Mono plus unfiltered music was clearly noisy, stereo helped, and
  **stereo + 250 Hz high-pass + -6 dB was clean** in a listening test. Frame size (20 ms vs 100 ms) made no difference,
  so per-frame publishing jitter is not a factor.
- Microphone gain at 1-2 m is low: speech peaked around **650 of 32768** (≈ -34 dBFS) while ambient noise sat at ~95,
  so a straight record-and-play loopback is faint and its noise floor is audible; about +20 dB of gain is needed.
- **Playback is silent when the phone app turns the voice assistant off**, even though the topic keeps accepting and
  echoing frames. This explained a confusing round of tests where nothing was audible but every API call succeeded.
  Verify sound with the microphone peak: ambient ≈ 95, playing ≈ 15,000.
- Volume sweep (80 / 120 / 160 / 200 of 200) at 16 kHz: **120** was the best compromise; louder distorted and quieter
  sounded thin. 44.1 kHz sounded worse than 16 kHz, and an EQ (+2 dB at 300 Hz, +4 dB at 3 kHz) or a lower high-pass
  (130 Hz instead of 250 Hz) were both inaudible, so the tone is limited by the speaker rather than the client.
- Separately from the SDK, a community Xbox receiver driver was built and tested for Linux input on one Bumi compute
  board. See [xbox-wireless-controller.md](xbox-wireless-controller.md) for the environment and limits.

### Not verified

- Executing the remaining `PublishCmd` actions (dances, teach/playback, `FALLTOSTAND`/`STANDTOFALL`) on a physical
  robot, and walking under `WALK` velocity commands on the ground.
- What the `format` field of `AudioStream` means beyond the observed value `2` (both capture and playback were `2`),
  and the semantics of `VideoStream.format` (observed `0` with 2 bytes per pixel).
- Publishing external video from this binding, feeding external audio to the built-in agent, wake words, and
  `SetSystemControl`.
- The meaning of `MotorState.error == 1`, seen on 13 of 21 motors while the robot was suspended.
- aarch64 low native wrapper build and controller runtime behavior.
- Behaviour when the application stops sending commands (does the robot stop by itself?).
- Callback threading limits (the callbacks are invoked on SDK threads; this binding copies/forwards but does not
  throttle them).
- Redistribution of the built native libraries (they embed the official BSD-3-Clause static library).
- LowController at runtime. It is exposed for parity with the official SDK; a managed runtime is not suited to
  hard-real-time loops.

## 中文

对照官方 SDK 3.1.0 验证（`noetix_sdk_bumi`，浅克隆，2026 年 11 月）。

### 已验证

| 检查项 | 结果 |
| --- | --- |
| 原生封装可针对官方 `libnoetix_sdk.a` + CycloneDDS 编译并链接 | 是，GCC 11.4 / Ubuntu 22.04 / x86_64，无警告 |
| 导出符号 | `libnoetix_bumi_c.so`：51 个 `noetix_*` 函数；`libnoetix_bumi_low_c.so`：7 个 |
| 同一封装使用 GCC 15 | **失败**：官方静态库中包含 GCC 11 LTO 字节码 |
| 将 High 和 Low 放进同一个库 | **失败**：两者都定义了 `noetix::robot_hardware_status_callback`；因此分成两个库 |
| 结构体大小/偏移、枚举值（C 头文件 ⇄ C# ⇄ 官方 `common.h`） | 单元测试 |
| 头文件中的每个函数都有 P/Invoke 和原生定义 | 单元测试 |
| .NET 在 Linux 上加载两个库并调用它们（版本、参数校验、错误消息、`CYCLONEDDS_URI` 的 `setenv`） | 7 个冒烟测试，Linux 容器 |
| CycloneDDS 能读取通过 `NoetixSdk.SetDdsConfig` 传入的配置 | 是（解析器针对该文件打印了一条警告） |
| High 原生封装可在 Bumi 算力板上构建 | 是，GCC 11.4 / Ubuntu 22.04 / aarch64；官方 SDK 提交 `95b264598ed671a76f7959a69c21b1b99f40c0b2` |
| C# 可在算力板上加载 High 原生封装 | 是，.NET Runtime 10.0.15 / aarch64；SDK 版本 `3.1.0`，C API 版本 `1` |
| C# 可在实体 Bumi 上初始化 `HighController` 并接收实时硬件状态 | 是，2026-11-18 在 ARM64 上进行了两次只读 [StatusMonitor](arm64-board-test.md) 运行：在五秒订阅窗口内分别收到 2,330 和 2,324 次回调；两次都以 `0` 退出 |
| C# 发布的高层动作可由实体机器人执行 | 是，2026-11-19 在 ARM64 算力板上：`Swing`（2 → 8 → 2）、`Shake`（2 → 9 → 2）和 `Cheer`（2 → 10 → 2）都执行了，并自行回到走路模式；最小踝关节位移为 `Swing` 的 0.373 rad，最大关节变化量为 1.971 rad（`Cheer`）；步骤、21 关节 id 映射和完整日志见 [motion-test.md](motion-test.md) |
| C# 在实体机器人上走通媒体通路 | 是，2026-11-19：8 通道 / 16 kHz 麦克风采集有真实信号，脱敏视频 640×480@30 且人脸已打码，发布的音调在播放话题上回传；见 [MediaTest](../samples/Noetix.Sdk.Bumi.MediaTest/README.md) |

### 观察到

- 在仅回环的隔离网络命名空间（`unshare -Urn`）中，配合社区 MuJoCo 仿真器
  （`bumi_mujoco`），C# 绑定能接收到 500 Hz 的 21 电机状态，但**仅在**仿真器的状态
  写入端声明了 2 ms 的 DDS deadline 之后。使用未修改的写入端时，官方 SDK 的读取端（C++ 和 C#）
  收到零个样本，而一个纯 Python 读取端仍然可用。
- HighController 的命令通路已在仿真器上验证：先 `Switch` 再 `Walk` 使 `workmode` 从
  1 → 2。更早的跟随距离结果是依据指令运动而非 MuJoCo 位姿计算的，
  并且显示的目标有独立的时间线。**这些结果不能验证跟随或间隙。**
  修订后的跟随原型仅在离线运行，使用来自实际仿真位姿的合成观测；
  它不测试 SDK 命令通路，也不代表相机感知或真实世界安全性。
- 在一次 15 秒的 x86_64 MuJoCo 脚本化接近/丢失场景中，修订后的原型测得机体到标记的最小
  中心距离为 1.816 m，在 100 帧模拟丢失期间输出零条命令，并在丢失期间
  使机体移动了 0.008 m。该标记没有碰撞体；这些结果不是碰撞
  测试，也不是安全距离。无头运行成功退出；WSLg 查看器完成了仿真，
  但在关闭时因原生内存错误而终止。
- 在没有可达机器人时，`HighController.Init()` 不会返回（60 秒后仍阻塞）。请在 UI 线程之外
  调用并设置超时。
- Ubuntu 22.04 上的 x86_64 构建链接的是 `libssl.so.3`，而不是官方 FAQ 所说的 `libssl1.1`。
- aarch64 上的 C# 版本探测没有初始化控制器或 DDS，也没有验证机器人状态。
  随后的只读算力板测试确实验证了实时状态接收。CycloneDDS 警告 DDS 配置中存在已弃用的隐式
  单位，但两次运行都在不更改配置或机器人服务的情况下收到了回调。
- 2026-11-19，机器人吊在吊架上时报告 `workmode` 30（`失能模式`）。先执行一次
  `Swing`，再以约 100 Hz 发布 `Default` —— 12 秒内共 1,000 次 `PublishCmd` 调用 —— 完成且无异常，
  状态回调持续（6,739 条消息），但没有任何关节移动超过 0.02 rad，`workmode` 保持 30。
  因此机器人处于失能状态时不会执行预设动作。未发送使其启用的命令（从 `workmode` 30 发送 `START`），
  所以硬件上的运动执行仍未验证。
- 2026-11-19（稍后的会话）机器人报告 `workmode` 2（`走路模式`）。`Switch> Swing` 序列加上
  约 100 Hz 的 `Default`（20 秒内共 1,166 次 `PublishCmd` 调用）同样没有产生任何变化：`workmode` 全程保持 2，
  没有任何关节移动超过 0.02 rad，同时正常收到 10,503 条状态消息。因此命令通路
  发布时没有错误，但该设置的运控器并不响应这些命令；原因尚未
  查明。仅执行 `Switch` 也没有离开走路模式，因此缺失的环节不是“需要就绪模式”这一前提条件。
- **这两次失败的根因：示例停止了发布。** `MotionTest` 每发送一个动作后就静默
  长达 6 秒，等待模式改变。厂商示例从不停止发布。把示例改为
  持续以流的形式发送 `PublishCmd`（周期 2 ms，空闲时发送 `Default`，动作样本混在
  流中）之后，预设动作便在实体机器人上执行了。
- 2026-11-19（C# SDK，ARM64 算力板，`MotionTest ... Switch>Swing 12 500`）：机器人以 `workmode` 2 启动，
  `Switch` 之后仍为 2，随后 `Swing` 使 `workmode` 2 → 8（`出厂预设动作 打招呼`），全部 21 个关节都移动了，其中手臂
  最多（电机 11 移动 1.712 rad，13 移动 1.535，10 移动 0.927，12 移动 0.488），共发布 2 条动作命令和 2,975 条
  `Default` 命令，收到 6,068 条状态消息。**C# 绑定的高层运动控制已在硬件上验证**，
  包括挥手预设动作。命令步骤、预期输出和剩余测试计划见
  [motion-test.md](motion-test.md)。
- 2026-11-19（相同设置，`MotionTest ... Cheer 12 500`）：`Cheer`（`出厂预设动作 欢呼`）使 `workmode` 2 → 10 → 2，
  并移动了全部 21 个关节，最大者移动 1.971 rad，共 1 条动作命令、2,987 条 `Default` 命令和 6,067 条状态
  消息。该预设在同一个 12 秒窗口内完成并回到走路模式，这说明预设动作
  完成后会自行退出。
- 2026-11-19（相同设置，`MotionTest ... Shake 12 500`）：`Shake`（`出厂预设动作 握手`）使 `workmode` 2 → 9 → 2；
  手臂最大变化量为 1.392 rad（`arm_l3`），腿部最大变化量为 0.945 rad（`leg_l1`）。
- 使用官方 21 关节 id 映射（0–3 左臂，4–9 左腿，10–13 右臂，14–19 右腿，20 腰部）时，
  示例现在会报告每个区域的最大偏差。在三个预设中，踝关节位移分别为 `Swing` 0.373 rad、
  `Shake` 0.591 rad 和 `Cheer` ≤0.621 rad，因此**`Swing` 是腿部移动最少的预设**。它们都不是
  纯手臂动作：黑盒平衡控制器总会涉及腿部（髋/膝约 0.9 rad）。
- 在同批运行中，21 个电机里有 13 个报告 `error=1`。官方故障表只列出 `02H`–`0EH`，因此 `1` 不是
  有文档记录的故障码；其含义仍然未知。`mean|tau|` 为 0.81–0.98 N·m。
- 麦克风**没有**接在算力板上：`/dev/snd` 只有 `pulseaudio` 打开了控制节点（没有采集节点），
  本地 `arecord` 录 2 s 得到纯静音（峰值为 0），而 `SubscribeInternalAudioCapture` 收到 8 通道、16 kHz、
  有真实信号的数据。通道 0–3 是头顶麦克风，4–5 是预留（峰值 1），6–7 是扬声器回采。
- 只要 `noetix-video-capture` 在发布，脱敏视频流就是原始 YUYV422 640×480@30（614,400 字节/帧，约 15 MB/s），
  人脸已打码。在该带算力板的机型上 `SubscribeInternalVideoCapture` 收到 0 帧。
- 用 `PublishExternalAudioPlaybackStream` 发布 1 s 440 Hz 音调后，`SubscribeInternalAudioPlayback` 回传了
  50 帧，且播放期间麦克风峰值从空闲时的 179 升到 12,795，说明扬声器确实出声。`GetVolume()` 为 0–200 中的 1，
  因此测试时临时调到 100，结束后已恢复。
- 机器人空闲时语音系统上报 `Sleeped`（`CmdSleeped`），外部播放仍然有效。
- 播放音质：播放话题规定是 **2 声道**，所以最初发单声道是错的；另外机身喇叭对低频支撑不住。
  单声道 + 未滤波音乐明显有杂音，改立体声有改善，**立体声 + 250 Hz 高通 + -6 dB 后听感干净**。
  帧长（20 ms 与 100 ms）没有差别，说明逐帧发布抖动不是原因。
- 麦克风在 1–2 米处的增益偏低：说话峰值约 **650 / 32768**（≈ -34 dBFS），环境底噪约 95，
  因此直接"录了再放"声音很轻且能听到底噪，需要约 +20 dB 增益。
- **手机 App 关闭语音助手后，外部音频播放也没有声音**，但播放话题仍然接收并回传帧。这解释了一轮"所有 API 都成功却没声音"
  的困惑测试。判断是否真的出声看麦克风峰值：环境约 95，播放时约 15,000。
- 音量扫描（0–200 中的 80 / 120 / 160 / 200，16 kHz）：**120** 最平衡，更高会破音、更低发虚。
  44.1 kHz 比 16 kHz 更差；EQ（300 Hz +2 dB、3 kHz +4 dB）与把高通从 250 Hz 降到 130 Hz 都听不出差别，
  说明音色受限于扬声器而非客户端。
- 与 SDK 无关，另有一个社区 Xbox 接收器驱动在某一台 Bumi 算力板上完成了 Linux 输入的构建与
  测试。环境与限制见 [xbox-wireless-controller.md](xbox-wireless-controller.md)。

### 未验证

- 在实体机器人上执行其余 `PublishCmd` 动作（舞蹈、示教/复现、`FALLTOSTAND`/`STANDTOFALL`），
  以及在地面上按 `WALK` 速度命令行走。
- `AudioStream` 的 `format` 字段在观测到的 `2` 之外的含义（采集和播放都是 `2`），以及 `VideoStream.format`
  的语义（观测到 `0`，每像素 2 字节）。
- 从本绑定发布外部视频、把外部音频送给内置智能体、唤醒词，以及 `SetSystemControl`。
- `MotorState.error == 1` 的含义，该值在机器人悬吊时于 21 个电机中的 13 个上出现。
- aarch64 上 low 原生封装的构建以及控制器的运行时行为。
- 应用程序停止发送命令时的行为（机器人会自行停止吗？）。
- 回调线程方面的限制（回调在 SDK 线程上调用；本绑定只做复制/转发，不做
  限流）。
- 构建出的原生库的再分发（它们内嵌了官方 BSD-3-Clause 静态库）。
- 运行时的 LowController。它为与官方 SDK 保持对等而暴露；托管运行时不适合
  硬实时循环。
