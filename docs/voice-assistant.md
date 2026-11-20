# 自建语音助手设计指南 / Building your own voice assistant

[中文](#中文) | [english](#english)

## 中文

本文给出在 Bumi EDU 上用 `MediaController` 自建语音助手的可行方案，基于 2026-11-19 / 11-20 在真机上
的实测数据。**这不是官方文档**，结论都来自本仓库的实际测试，标注方式见下。

标注约定：**[官方]** = Noetix 文档或 SDK 源码明确说明；**[实测]** = 本仓库在真机上的观察；
**[推断]** = 尚未验证的推测。

> **隐私声明（重要）**：当前固件下可用的方案，**麦克风音频仍然会送进内置智能体（豆包）**。
> 内置助手的回复可以被静音，但语音输入无法在不破坏扬声器功能的前提下切断。
> 如果可接受的前提是"语音不会离开设备"，本方案不适用。

### 1. 结论速览

想让机器人播自己的语音、同时不被内置助手打断，**只有一条路可用**：

| 方案 | `mic→agent` | `agent→喇叭` | 唤醒词 | 自己的播放 | 语音是否进内置助手 |
| --- | --- | --- | --- | --- | --- |
| **A. 只静音（可用）** | 开 | **关** | 仍会触发（无声） | ✅ 正常 | ⚠️ **会** |
| B. 彻底断开 | **关** | 关 | 不触发 | ❌ **全死** | 不会 |

**B 方案不可用的原因**：`set_internal_capture_audio_data_to_agent_enable(false)` 在当前固件上是
**整个语音子系统的总开关**，不是单纯的路由开关。关掉它之后，唤醒检测和外部音频播放一起失效，
`TO_WAKEUP` 和重新设置路由都无法恢复。

### 2. 架构

```text
┌─────────────── 算力板（你的程序）───────────────┐
│                                                │
│  SubscribeInternalAudioCapture   ← 8ch / 16kHz │
│        │  取 ch0–3（头顶 4 麦），混成单声道      │
│        ▼                                       │
│  增益 +15~20 dB（远场补偿）                      │
│        ▼                                       │
│  VAD / 唤醒词 / ASR / LLM / TTS（你的实现）      │
│        ▼                                       │
│  高通 250 Hz + 降 6 dB（小喇叭不破音）            │
│        ▼                                       │
│  PublishExternalAudioPlaybackStream  2ch/16kHz │
└────────┼───────────────────────────────────────┘
         ▼
   DDS                          ┌──────────────────────────┐
   external_playback  ────────► │ 运控板语音模块            │
   internal_capture   ◄──────── │  · 麦克风阵列             │
                                │  · 内置智能体（已静音）    │
                                │  · 扬声器                 │
                                └──────────────────────────┘
```

**[官方]** 唤醒词、语音识别、对话和 TTS 都在运控板的语音模块里；算力板只通过 DDS 交换音频。
运控板不对用户开放，不要尝试访问。

### 3. 开关表

| 接口 | [官方] 语义 | [实测] 效果 |
| --- | --- | --- |
| `SetInternalCaptureAudioDataToAgentEnable` | 内部麦克风采集音频流输入到 agent | 关掉 = **唤醒失效 + 播放失效**（总开关） |
| `SetInternalAgentAudioDataToPlaybackEnable` | agent 音频流输出到内部扬声器 | 关掉 = 豆包静音，**"我在！"也不响**，播放不受影响 |
| `SetAudioCueEnable` | 提示音是否开启 | 关掉无副作用 |
| `SetExternalCustomAudioDataToPlaybackEnable` | 外部音频流输出到内部扬声器 | 保持开启，这是你的发声通道 |
| `AddWakeupWords` / `ResetWakeupWords` | 添加 / 恢复出厂唤醒词 | **只能加不能删**，唯一删除手段是整体恢复出厂 |

#### 隔离实测（关键）

每组都读回确认，播放判定用麦克风回采峰值：

| `mic→agent` | `agent→喇叭` | `cue` | 麦克风峰值 | 播放 |
| --- | --- | --- | --- | --- |
| 开 | 开 | 开 | 6,249 | ✅ |
| 开 | **关** | 开 | 6,663 | ✅ |
| 开 | 开 | **关** | 6,479 | ✅ |
| **关** | 开 | 开 | 232 | ❌ |
| 关 | 关 | 关 | 358 | ❌ |
| 关 + `TO_WAKEUP` | 开 | 开 | 234 | ❌ |

只有 `mic→agent = 关` 会让播放停摆，且无法用唤醒或重新设路由恢复。

### 4. 实测数据

| 项 | 值 |
| --- | --- |
| 采集格式 | 8 通道 / 16000 Hz / `format=2`（16 bit 小端交错） |
| 通道布局 | ch0–3 头顶麦克风，ch4–5 预留（峰值 1），ch6–7 扬声器回采 |
| 播放格式 | 2 通道 / 16000 Hz / `format=2` |
| 麦克风远场增益 | 1–2 m 说话峰值约 650/32768（≈ -34 dBFS），环境底噪约 95 → 需 **+15~20 dB** |
| 播放音质 | 单声道或未滤波音乐明显有杂音；立体声 + 250 Hz 高通 + -6 dB 后干净 |
| 音量甜点 | 200 中的 **120**（80/120/160/200 对比），更高破音、更低发虚 |
| 帧长 | 20 ms 与 100 ms 无差别 |
| 44.1 kHz | 比 16 kHz 更差（模块内部重采样） |
| 播放回声 | 正常播放时回采峰值约 19,000；静音时 1–2 |

#### 唤醒行为对照

| 状态 | 说"布米" | 系统状态事件 | 是否出声 |
| --- | --- | --- | --- |
| 全开（基线） | 说了 | `Wakeuped reason=AudioWakeuped` | ✅ 会答"我在！" |
| 路线 A（只静音） | 说了 | `Wakeuped reason=AudioWakeuped` | ❌ **不出声** |
| 彻底断开 | 说了 | **0 个事件** | ❌ |

路线 A 下，整段 23.4 秒录音中扬声器回采 ch6/7 峰值始终为 1–2，确认唤醒应答也被静音。

### 5. 三个必须知道的坑

1. **`mic→agent` 是总开关**。名字看起来只是"要不要把麦克风音频给 agent"，实际会连唤醒检测和
   扬声器一起关掉。想要扬声器就别关它。
2. **开关不恢复 = 机器人永久哑巴**。程序退出、崩溃或异常终止后，`agent→喇叭=False` 会留在设备上，
   App 也救不回来，只能用 SDK 重新设置。必须在 `finally` / `SIGTERM` 里恢复。
3. **唤醒词删不掉**。"布米"和"停止运动"无法单独移除，只能 `ResetWakeupWords` 整体恢复出厂。
   所以自建助手必须接受"内置助手仍在监听"这个事实。

### 6. 设计模板

```csharp
var media = MediaController.Instance;
media.Init();

// 保存原值，退出时必须恢复
var original = (
    AgentOut: media.GetInternalAgentAudioDataToPlaybackEnable(),
    Cue: media.GetAudioCueEnable(),
    MicToAgent: media.GetInternalCaptureAudioDataToAgentEnable());

try
{
    // 路线 A：只静音，不动 mic→agent
    media.SetInternalAgentAudioDataToPlaybackEnable(false);
    media.SetAudioCueEnable(false);
    media.SetExternalCustomAudioDataToPlaybackEnable(true);

    media.SubscribeInternalAudioCapture(onMic);   // 8ch/16k
    // ... 你自己的 VAD / ASR / LLM / TTS ...
}
finally
{
    media.SetInternalAgentAudioDataToPlaybackEnable(original.AgentOut);
    media.SetAudioCueEnable(original.Cue);
    media.SetInternalCaptureAudioDataToAgentEnable(original.MicToAgent);
}
```

播放时：取 ch0–3 混成单声道、增益 +15~20 dB、高通 250 Hz、降 6 dB，最后以 2 通道 16 kHz 发布。

### 7. 隐私与合规

- **语音输入仍会进入内置智能体**。`mic→agent` 必须保持开启，否则扬声器失效。这意味着你说的话会被
  官方 agent 处理，可能经过云端。
- **对外交付时必须声明这一点**，否则属于合规问题。
- 可在启动时用 `SetSystemControl(ToSleep)` 尝试让模块休眠，但这会让播放一起失效（等同于方案 B），
  不能作为"隐私保护 + 保留播放"的手段。
- 本文档只描述技术事实，不代表厂商认可这种用法。

### 8. 尚未验证

| 项 | 风险 |
| --- | --- |
| 长时间稳定性（≥1 小时连续录放） | DDS 内存增长、帧漂移 |
| 异常退出后的状态残留 | 需要守护逻辑或启动时强制恢复 |
| App 与 SDK 开关的相互覆盖 | 用户在 App 里关语音会导致播放全死 |
| 端到端延迟 | 决定能否做实时对话 |
| "停止运动"唤醒词是否仍有效 | **安全项**；路线 A 保留了 KWS，[推断] 应仍有效，但未验证 |
| 多进程同时订阅的压测 | 双进程共存未发现问题，但未做压力测试 |

### 9. 复现

```bash
root="$HOME/noetix-csharp-arm64-build-20261118"
export LD_LIBRARY_PATH="$root/build:$root/official-sdk/dds/aarch64/lib"
config="$root/official-sdk/config/dds.xml"

# 只读当前状态
dotnet MediaTest.dll "$config" config

# 路线 A：只静音（保留 mic→agent）
dotnet MediaTest.dll "$config" wakewords get
dotnet MediaTest.dll "$config" system wakeup

# 播放测试（验证扬声器仍可用）
dotnet MediaTest.dll "$config" play 5
```

`MediaTest` 的 `config` 命令会打印全部开关和当前唤醒词，改任何开关前先读一遍。

## english

This document describes how to build a voice assistant on a Bumi EDU with `MediaController`, based on measurements
taken on a real robot on 2026-11-19 and 11-20. **It is not official documentation**; every conclusion comes from this
repository's testing.

Markers: **[official]** = stated by Noetix docs or SDK source; **[measured]** = observed on our robot;
**[inference]** = not yet verified.

> **Privacy notice (important)**: in the only workable configuration on the current firmware, **microphone audio still
> reaches the built-in agent (Doubao)**. Its replies can be muted, but the voice input cannot be cut without also
> losing the speaker. If "voice must never leave the device" is a hard requirement, this design does not apply.

### 1. Summary

To play your own speech and not be interrupted by the built-in assistant, exactly one configuration works:

| Option | `mic→agent` | `agent→speaker` | Wake word | Your playback | Voice reaches the agent |
| --- | --- | --- | --- | --- | --- |
| **A. Mute only (usable)** | on | **off** | still fires (silent) | yes | **yes** |
| B. Full disconnect | **off** | off | does not fire | **dead** | no |

**Why option B does not work**: on this firmware `set_internal_capture_audio_data_to_agent_enable(false)` is a
**master switch for the whole audio subsystem**, not a plain routing flag. Wake detection and external playback both
stop, and neither `TO_WAKEUP` nor re-setting the routing switches restores them.

### 2. Architecture

```text
compute board (your program)
  SubscribeInternalAudioCapture   <- 8ch / 16 kHz
    take ch0-3 (the four head microphones), downmix to mono
    +15-20 dB gain (far-field compensation)
    VAD / wake word / ASR / LLM / TTS (your implementation)
    250 Hz high-pass + -6 dB (the small speaker distorts otherwise)
  PublishExternalAudioPlaybackStream  2ch / 16 kHz
        |
        v  DDS
motion board voice module: microphone array, built-in agent (muted), speaker
```

**[official]** Wake word, speech recognition, dialog and TTS all live in the motion board's voice module; the compute
board only exchanges audio over DDS. The motion board is not open to users.

### 3. Switches

| API | [official] meaning | [measured] effect |
| --- | --- | --- |
| `SetInternalCaptureAudioDataToAgentEnable` | internal microphone audio into the agent | off = **wake detection and playback both die** (master switch) |
| `SetInternalAgentAudioDataToPlaybackEnable` | agent audio out to the internal speaker | off = agent silent, **including "我在！"**, playback unaffected |
| `SetAudioCueEnable` | whether the prompt tone plays | off has no side effects |
| `SetExternalCustomAudioDataToPlaybackEnable` | external audio out to the internal speaker | keep on; this is your output path |
| `AddWakeupWords` / `ResetWakeupWords` | add / restore factory wake words | **add only**; the only way to remove is a factory reset |

#### Isolation measurements

Each row was read back to confirm, and playback was judged by the microphone peak:

| `mic→agent` | `agent→speaker` | `cue` | Mic peak | Playback |
| --- | --- | --- | --- | --- |
| on | on | on | 6,249 | yes |
| on | **off** | on | 6,663 | yes |
| on | on | **off** | 6,479 | yes |
| **off** | on | on | 232 | no |
| off | off | off | 358 | no |
| off + `TO_WAKEUP` | on | on | 234 | no |

Only `mic→agent = off` kills playback, and waking the module or re-arming the routes does not bring it back.

### 4. Measurements

| Item | Value |
| --- | --- |
| Capture format | 8 channels / 16000 Hz / `format=2` (16-bit little-endian, interleaved) |
| Channel layout | ch0-3 head microphones, ch4-5 reserved (peak 1), ch6-7 speaker echo |
| Playback format | 2 channels / 16000 Hz / `format=2` |
| Microphone far-field gain | speech at 1-2 m peaks around 650/32768 (about -34 dBFS) against ambient 95, so **+15-20 dB** is needed |
| Playback quality | mono or unfiltered music is clearly noisy; stereo + 250 Hz high-pass + -6 dB is clean |
| Volume sweet spot | **120** of 200 (compared 80/120/160/200); louder distorts, quieter is thin |
| Frame size | 20 ms and 100 ms made no difference |
| 44.1 kHz | worse than 16 kHz (the module resamples internally) |
| Playback echo | about 19,000 while playing, 1-2 when silent |

#### Wake behaviour

| State | Said "布米" | System status | Audible |
| --- | --- | --- | --- |
| All on (baseline) | yes | `Wakeuped reason=AudioWakeuped` | yes, replies "我在！" |
| Option A (mute only) | yes | `Wakeuped reason=AudioWakeuped` | **no** |
| Full disconnect | yes | **0 events** | no |

Under option A the speaker echo channels stayed at 1-2 for the whole 23.4 s recording, which confirms the wake
response is silenced too.

### 5. Three pitfalls

1. **`mic→agent` is a master switch.** The name suggests routing only, but it also takes wake detection and the
   speaker down. If you want sound, do not turn it off.
2. **Not restoring the switches leaves the robot mute.** After an exit, crash or kill, `agent→speaker=False` stays on
   the device and the phone app cannot undo it; only the SDK can. Restore in `finally` and on `SIGTERM`.
3. **Wake words cannot be removed.** "布米" and "停止运动" cannot be deleted individually; only
   `ResetWakeupWords` restores the factory list. A custom assistant has to accept that the built-in one keeps
   listening.

### 6. Template

```csharp
var media = MediaController.Instance;
media.Init();

var original = (
    AgentOut: media.GetInternalAgentAudioDataToPlaybackEnable(),
    Cue: media.GetAudioCueEnable(),
    MicToAgent: media.GetInternalCaptureAudioDataToAgentEnable());

try
{
    media.SetInternalAgentAudioDataToPlaybackEnable(false);   // mute the agent
    media.SetAudioCueEnable(false);
    media.SetExternalCustomAudioDataToPlaybackEnable(true);   // keep your output

    media.SubscribeInternalAudioCapture(onMic);               // 8ch/16k
    // your VAD / ASR / LLM / TTS
}
finally
{
    media.SetInternalAgentAudioDataToPlaybackEnable(original.AgentOut);
    media.SetAudioCueEnable(original.Cue);
    media.SetInternalCaptureAudioDataToAgentEnable(original.MicToAgent);
}
```

For playback: downmix ch0-3, apply +15-20 dB, 250 Hz high-pass and -6 dB, then publish as 2 channels at 16 kHz.

### 7. Privacy and compliance

- **Voice input still reaches the built-in agent.** `mic→agent` has to stay on or the speaker dies, which means what
  you say is processed by the vendor agent and may go to the cloud.
- **Say so when delivering this to anyone else**, otherwise it is a compliance problem.
- `SetSystemControl(ToSleep)` silences everything including playback, so it is not a "privacy plus playback" option.
- This document records technical facts only; it does not imply vendor endorsement.

### 8. Not verified

| Item | Risk |
| --- | --- |
| Long-run stability (1 h of continuous record/play) | DDS memory growth, frame drift |
| State left behind by an abnormal exit | needs a guard, or a restore at startup |
| App and SDK switches overriding each other | turning voice off in the app kills playback |
| End-to-end latency | decides whether real-time dialog is possible |
| Whether "停止运动" still works | **safety**; option A keeps KWS alive so [inference] it should, but untested |
| Multi-process subscription under load | two processes coexisted, but no stress test |

### 9. Reproduction

```bash
root="$HOME/noetix-csharp-arm64-build-20261118"
export LD_LIBRARY_PATH="$root/build:$root/official-sdk/dds/aarch64/lib"
config="$root/official-sdk/config/dds.xml"

dotnet MediaTest.dll "$config" config      # read every switch first
dotnet MediaTest.dll "$config" play 5      # verify the speaker still works
```

Read the switches with `config` before changing any of them.
