# Bumi 机身扬声器音质实测报告 / Audio quality measurements of the Bumi on-board speaker

[中文](#中文) | [english](#english)

## 中文

本文记录在 **Bumi EDU 真机**上对机身扬声器做的客观测量，用于说明"客户听到断续/杂音"这一现象在
**软件链路**与**扬声器硬件**之间分别占多少。文中数据均可按第 3 节的方法复现。

标注约定：**[官方]** 表示 Noetix 文档/源码明确说明；**[实测]** 表示在真机上观察/测量得到；
**[推断]** 表示尚未验证的推测；**[转述]** 表示厂家技术人员的口头反馈，非书面结论。

相关文档：[voice-assistant.md](voice-assistant.md)（自建语音助手方案）、
[verification.md](verification.md)（整体验证台账）。

### 1. 结论速览

| 问题 | 结论 | 依据 |
| --- | --- | --- |
| 服务端下发的语音数据是否有空洞 | **没有**。10.6 s 的回复实际跨度 10.36 s，帧内最大间隔 67 ms（帧长 60 ms） | [实测] 4.4 节 |
| 客户端播放链路是否丢帧 | **没有**。10 s 连续单音录音能量全程连续，仅起播处 60 ms | [实测] 4.5 节 |
| 断续是否由网络/客户端缓冲造成 | **不是**。300 ms 前导缓冲、立即灌满、100 ms 大帧三种策略均无改善 | [实测] 4.5 节 |
| 扬声器能否正常还原低频 | **不能**。180 Hz 时谐波比基波响 10.5 dB；440 Hz 比 1 kHz 低约 30 dB | [实测] 4.2、4.3 节 |
| 提高音量能否更响 | **几乎不能**。音量 60→180（参数 3 倍）声学输出仅 +4 dB | [实测] 4.1 节 |
| 是否可通过音色后处理改善 | 数字域可降低削波失真（THD 22.5%→0.12%），但**主观听感未改善甚至更差** | [实测] 4.6 节 |

**总结：软件侧的数据链路是干净的，可听到的问题集中在扬声器/功放的物理能力上。**

### 2. 被测对象

| 项 | 值 |
| --- | --- |
| 机型 | Bumi EDU（算力板 Jetson Orin Nano） |
| 原生 SDK | `libnoetix_bumi_c.so`，SDK 3.1.0，GCC 11 构建 |
| 系统 | Ubuntu 22.04 / aarch64 |
| 播放话题格式 | 2 声道 / 16000 Hz / `format=2`（16 bit 小端交错）[官方] |
| 采集格式 | 8 声道 / 16000 Hz；ch0–3 头顶麦克风，ch4–5 预留，ch6–7 扬声器回采 [官方] |
| 音量范围 | 0–200，测试时回读确认生效 |
| 厂家反馈 | 音量较高时的杂音"暂时处理不了" [转述] |

### 3. 测量方法（可复现）

测量分两个层次：**数字域**（只跑音色处理链路，不碰硬件）和**声学域**（播放后用机身麦克风录回来）。

#### 3.1 生成测试信号

```bash
# 10 秒 1 kHz 正弦，-6 dBFS
ffmpeg -y -f lavfi -i "sine=frequency=1000:duration=10:sample_rate=16000" \
       -af "volume=-6dB" -ac 1 -c:a pcm_s16le tone10s.wav
```

> 频点选择很重要：**不要用 440 Hz 或更低的频点做播放连续性测试**。实测 440 Hz 在机身上的声学
> 输出比 1 kHz 低约 30 dB（见 4.3 节），"听不到"与"断开"无法区分，会得出错误结论。

#### 3.2 播放并同步录音

用仓库自带的媒体示例（不依赖本文提到的临时工具）：

```bash
export LD_LIBRARY_PATH=/path/to/native/build:$LD_LIBRARY_PATH
dotnet MediaTest.dll /path/to/config/dds.xml playfile tone10s.wav /tmp/out
```

示例会把发出的帧数、麦克风录音（`record-*.wav`）一并输出。录音为 8 声道，取 ch0–3 求平均即得单声道。

#### 3.3 分析

- **缺口统计**：把录音切成 20 ms 窗口算 RMS，低于峰值窗口 −20 dB 的连续窗口即记为一次"缺口"。
- **失真（THD）**：对稳态段做单频点 Goertzel，取 2–8 次谐波相对基波的能量比。

> **THD 的局限**：机身麦克风路径自身就有约 1.7% 的失真底，因此 1 kHz 上 **1.7%–2.6% 之间的差异
> 不具区分意义**；只有数量级的变化（如 22% → 0.1%，或 377% → 69%）才可信。

### 4. 测量结果

#### 4.1 音量扫描（1 kHz，-6 dBFS 输入，250 Hz 高通 + −6 dB）

| 音量 | 声学峰值 | 底噪 | THD |
| --- | --- | --- | --- |
| 60 | −15.5 dBFS | −63.3 dBFS | 1.65% |
| 100 | −13.0 dBFS | −62.7 dBFS | 1.78% |
| 140 | −11.6 dBFS | −63.4 dBFS | 1.78% |
| 180 | −11.5 dBFS | −64.1 dBFS | 1.72% |

读数均经 `GetVolume()` 回读确认已生效。**音量参数扩大 3 倍，声学输出只增加 4 dB**，说明功放在
音量 100 以上已进入明显压缩区：继续提高音量不会更响，只会增加扬声器负担。

#### 4.2 低频段（180 Hz，音量 140）

| 高通设置 | 基波 RMS | THD | 6 次谐波相对基波 |
| --- | --- | --- | --- |
| 250 Hz / 2 阶（默认） | −42.5 dBFS | **376.9%** | **+10.5 dB** |
| 200 Hz / 4 阶 | −37.8 dBFS | **547.2%** | **+12.9 dB** |
| 300 Hz / 4 阶 | −57.8 dBFS | 69.3% | −7.9 dB |

**谐波比基波更响**意味着：扬声器无法线性还原该频率，把能量转成了谐波。听感上即"沙沙"或"破"。
把高通提高到 300 Hz / 4 阶后，该频点的失真产物下降约 15 dB。

#### 4.3 声学频响（音量 100）

| 频点 | 声学 RMS |
| --- | --- |
| 1 kHz | −16.9 dBFS |
| 440 Hz | −44.4 dBFS |

相差约 **27 dB**，与 4.2 节一致：机身扬声器在 500 Hz 以下几乎没有有效输出。

#### 4.4 服务端下发的语音数据连续性（实时对话，音量 100）

| 回复 | 音频时长 | 实际跨度 | 帧内最大间隔 |
| --- | --- | --- | --- |
| 第 1 句 | 10.6 s（176 帧） | 10.364 s | 67 ms |
| 第 2 句 | 2.2 s（36 帧） | 1.979 s | 66 ms |
| 第 3 句 | 2.6 s（43 帧） | 2.395 s | 71 ms |

帧长本身 60 ms，最大间隔 71 ms 表示最坏情况只比标称晚 11 ms。**实际跨度与音频时长基本相等，
说明下行数据中没有空洞。** 前导缓冲 300 ms 足以吸收该抖动。

#### 4.5 播放链路连续性（10 s 连续单音，音量 100）

| 项 | 结果 |
| --- | --- |
| 声学峰值 / RMS | −11.2 / −16.9 dBFS |
| THD | 2.11%（≈麦克风路径底线） |
| 缺口次数 | **1 次** |
| 缺口总时长 | 60 ms |
| 首次缺口位置 | 0.0 s（起播瞬间） |

10 秒内**除起播外没有任何缺口**。以下三种播放策略均做过对比，无改善：
按墙钟逐帧推送、立即灌满、100 ms 大帧。

#### 4.6 音色后处理的数字域效果

| 处理 | 数字域效果 | 主观听感 |
| --- | --- | --- |
| 峰值限幅（−3 dBFS）替代硬削波 | THD **22.46% → 0.12%**，峰值精确限制在 −3.0 dBFS | 未改善（正常语音电平下不触发限幅） |
| 3 kHz 高架 +4 dB | 拐点处实测 **+2.0 dB**（RBJ 曲线特性），符合设计 | **变薄、更刺**，评价为负优化 |
| 高通 250 Hz/2 阶 → 300 Hz/4 阶 | 180 Hz 处多衰减 **11 dB** | 人声**变薄**，评价为负优化 |

结论：数字域处理确实按预期工作，但在这台设备的扬声器上换不来更好的听感。

### 5. 希望厂家协助确认的事项

1. **同一段 10 s / 1 kHz 单音在本机型上是否连续出声**
   若厂方样机也出现断点，则问题在扬声器/功放固件侧；若连续，请对比我们提供的录音定位差异。
2. **音量 100 以上功放压缩是否为设计行为**
   实测 60→180 仅 +4 dB。若为限幅保护，请给出建议工作音量区间。
3. **500 Hz 以下无有效输出是否为该型号扬声器的规格上限**
   实测 180 Hz 失真 377%、440 Hz 比 1 kHz 低 27 dB。若是，请在文档中标注可用频带，
   我们据此在 TTS 侧选音色。

### 6. 未验证 / 待确认

- [推断] 扬声器的频率下限与最大不失真声压级（本文只给出相对关系，未做绝对声压标定）。
- 不同固件版本之间的差异（本文仅覆盖 SDK 3.1.0 对应的固件）。
- 长时间（≥1 小时）连续播放下的热漂移与稳定性。
- 是否存在可用的外部音频输出（算力板只有 HDMI 与板载模拟输出，均未接至机身扬声器）。

## english

This document records objective measurements of the on-board speaker of a **Bumi EDU**, to separate how much
of the "choppy / noisy" complaint comes from the **software chain** and how much from the **speaker hardware**.
Every number can be reproduced with the method in section 3.

Labels: **[official]** stated by Noetix docs/source; **[measured]** observed on the real robot;
**[inference]** not verified; **[relayed]** verbal feedback from the vendor's technicians, not a written conclusion.

Related: [voice-assistant.md](voice-assistant.md) (custom voice assistant),
[verification.md](verification.md) (overall verification ledger).

### 1. Summary

| Question | Answer | Basis |
| --- | --- | --- |
| Does the server stream contain holes? | **No.** A 10.6 s reply spanned 10.36 s, worst intra-reply gap 67 ms against 60 ms frames | [measured] 4.4 |
| Does the client playback path drop frames? | **No.** A 10 s continuous tone recorded continuous energy, with a 60 ms gap only at playback onset | [measured] 4.5 |
| Is the choppiness caused by network or client buffering? | **No.** 300 ms lead buffer, immediate flooding and 100 ms frames all made no difference | [measured] 4.5 |
| Can the speaker reproduce low frequencies? | **No.** At 180 Hz the harmonics are 10.5 dB louder than the fundamental; 440 Hz sits about 30 dB below 1 kHz | [measured] 4.2, 4.3 |
| Does raising the volume make it louder? | **Barely.** Volume 60 to 180 (3x the parameter) gained only 4 dB acoustically | [measured] 4.1 |
| Can tone shaping improve it? | In the digital domain yes (THD 22.5% -> 0.12% against clipping), but **subjective quality did not improve and got worse** | [measured] 4.6 |

**In short: the software data path is clean; what remains audible is the physical limit of the speaker and its amplifier.**

### 2. Test object

| Item | Value |
| --- | --- |
| Model | Bumi EDU (Jetson Orin Nano compute board) |
| Native SDK | `libnoetix_bumi_c.so`, SDK 3.1.0, built with GCC 11 |
| System | Ubuntu 22.04 / aarch64 |
| Playback topic format | 2 channels / 16000 Hz / `format=2` (16-bit little-endian interleaved) [official] |
| Capture format | 8 channels / 16000 Hz; ch0-3 head microphones, ch4-5 reserved, ch6-7 speaker return [official] |
| Volume range | 0-200, read back to confirm every change |
| Vendor feedback | Noise at higher volume "cannot be fixed for now" [relayed] |

### 3. Method (reproducible)

Two levels: **digital** (run the tone chain only, no hardware) and **acoustic** (play, then record with the
robot's own microphones).

#### 3.1 Generate the test signal

```bash
# 10 s of 1 kHz at -6 dBFS
ffmpeg -y -f lavfi -i "sine=frequency=1000:duration=10:sample_rate=16000" \
       -af "volume=-6dB" -ac 1 -c:a pcm_s16le tone10s.wav
```

> Choosing the frequency matters: **do not use 440 Hz or lower for continuity tests.** On this robot, 440 Hz
> measures about 30 dB below 1 kHz (section 4.3), so "inaudible" and "dropped out" cannot be told apart.

#### 3.2 Play and record simultaneously

Use the media sample shipped in this repository (no dependency on the throwaway tool used here):

```bash
export LD_LIBRARY_PATH=/path/to/native/build:$LD_LIBRARY_PATH
dotnet MediaTest.dll /path/to/config/dds.xml playfile tone10s.wav /tmp/out
```

It reports the published frame count and writes the microphone recording (`record-*.wav`); the recording has
8 channels, and averaging ch0-3 gives mono.

#### 3.3 Analysis

- **Gap detection**: split the recording into 20 ms windows, compute RMS, and count contiguous windows below
  peak-window minus 20 dB as one gap.
- **THD**: single-bin Goertzel over a steady window, ratio of harmonics 2-8 to the fundamental.

> **Limitation of the THD figure**: the microphone path itself has about 1.7% distortion, so differences
> between **1.7% and 2.6% at 1 kHz are not meaningful**. Only order-of-magnitude changes (22% -> 0.1%, or
> 377% -> 69%) can be trusted.

### 4. Results

#### 4.1 Volume sweep (1 kHz, -6 dBFS input, 250 Hz high-pass, -6 dB)

| Volume | Acoustic peak | Noise floor | THD |
| --- | --- | --- | --- |
| 60 | -15.5 dBFS | -63.3 dBFS | 1.65% |
| 100 | -13.0 dBFS | -62.7 dBFS | 1.78% |
| 140 | -11.6 dBFS | -63.4 dBFS | 1.78% |
| 180 | -11.5 dBFS | -64.1 dBFS | 1.72% |

Every value was confirmed by reading `GetVolume()` back. **Tripling the volume parameter gained 4 dB**, which
means the amplifier is already compressing above volume 100: more volume does not produce more sound, only
more strain.

#### 4.2 Low band (180 Hz, volume 140)

| High-pass | Fundamental RMS | THD | 6th harmonic vs fundamental |
| --- | --- | --- | --- |
| 250 Hz / 2nd order (default) | -42.5 dBFS | **376.9%** | **+10.5 dB** |
| 200 Hz / 4th order | -37.8 dBFS | **547.2%** | **+12.9 dB** |
| 300 Hz / 4th order | -57.8 dBFS | 69.3% | -7.9 dB |

**Harmonics louder than the fundamental** means the driver cannot reproduce that frequency linearly and turns
the energy into harmonics, which is heard as roughness or break-up. Raising the high-pass to 300 Hz / 4th order
cut the distortion products at that frequency by about 15 dB.

#### 4.3 Acoustic response (volume 100)

| Frequency | Acoustic RMS |
| --- | --- |
| 1 kHz | -16.9 dBFS |
| 440 Hz | -44.4 dBFS |

A 27 dB difference, consistent with section 4.2: the on-board speaker has almost no useful output below 500 Hz.

#### 4.4 Continuity of the speech stream from the server (live conversation, volume 100)

| Reply | Audio length | Wall span | Worst gap |
| --- | --- | --- | --- |
| 1 | 10.6 s (176 frames) | 10.364 s | 67 ms |
| 2 | 2.2 s (36 frames) | 1.979 s | 66 ms |
| 3 | 2.6 s (43 frames) | 2.395 s | 71 ms |

Frames are 60 ms, so a 71 ms gap is 11 ms behind nominal at worst. **The wall span matches the audio length, so
the downstream stream has no holes**, and the 300 ms lead buffer absorbs that jitter comfortably.

#### 4.5 Continuity of the playback path (10 s continuous tone, volume 100)

| Item | Result |
| --- | --- |
| Acoustic peak / RMS | -11.2 / -16.9 dBFS |
| THD | 2.11% (at the microphone floor) |
| Gaps | **1** |
| Total gap time | 60 ms |
| First gap | 0.0 s (playback onset) |

**No gaps at all across 10 seconds other than the onset.** Three playback strategies were compared with no
difference: wall-clock pacing per frame, immediate flooding, and 100 ms frames.

#### 4.6 Digital-domain effect of tone shaping

| Processing | Digital effect | Subjective result |
| --- | --- | --- |
| Peak limiter (-3 dBFS) instead of hard clamp | THD **22.46% -> 0.12%**, peak exactly -3.0 dBFS | No improvement (never engages at normal speech level) |
| 3 kHz high shelf, +4 dB setting | **+2.0 dB** at the corner, as the RBJ curve dictates | **Thinner and harsher**, judged a regression |
| High-pass 250 Hz/2nd -> 300 Hz/4th | 11 dB more attenuation at 180 Hz | Voice **thinner**, judged a regression |

The processing does what it is designed to do; on this speaker it does not buy a better listening result.

### 5. Requests for the vendor

1. **Is a 10 s / 1 kHz tone continuous on this model?** If the vendor's own unit also drops out, the issue is in
   the speaker/amplifier firmware; if not, compare against the recording we can provide.
2. **Is compression above volume 100 intended?** We measured only +4 dB from 60 to 180. If it is limiter
   protection, please state the recommended operating range.
3. **Is the lack of output below 500 Hz the specification of this speaker?** We measured 377% THD at 180 Hz and
   a 27 dB gap at 440 Hz. If so, please document the usable band so we can pick a suitable TTS voice.

### 6. Not verified

- [inference] The lower frequency limit and maximum undistorted SPL of the speaker; this document reports only
  relative values, not calibrated absolute SPL.
- Differences across firmware versions (only the firmware shipped with SDK 3.1.0 was measured).
- Thermal drift and stability under an hour or more of continuous playback.
- Whether an alternative audio output exists: the compute board exposes only HDMI and an on-board analog sink,
  neither of which is wired to the body speaker.
