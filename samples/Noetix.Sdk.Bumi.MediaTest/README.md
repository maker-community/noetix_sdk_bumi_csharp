# MediaTest

[english](#english) | [中文](#中文)

## english

Exercises the media path (microphone, speaker, camera) through `MediaController`. Nothing here moves the robot: it
subscribes to media topics and, for the playback test, publishes a short tone.

```text
MediaTest [path-to-dds.xml] [status|record|play|video|all] [seconds] [output-dir]
```

| Command | What it does |
| --- | --- |
| `status` | Subscribes to system status/errors, microphone audio and both video topics; prints a summary |
| `record` | Same as `status`, focused on saving the microphone audio as a WAV |
| `play` | Publishes a 1 s 440 Hz tone to the playback topic, then reports what came back |
| `video` | Same as `status`, focused on camera frames |
| `all` | Collects status + audio + video, then plays the tone |

Recorded audio is written as 16-bit PCM WAV and the first video frame of each stream is written next to it, so you can
inspect both outside the SDK.

## Measured result (Bumi EDU compute board, 2026-11-19)

| Path | Result |
| --- | --- |
| `SubscribeInternalAudioCapture` | 496 frames / 5 s, **8 channels, 16 kHz, format=2**, real signal |
| Channel layout | ch0–3 = the four head microphones, ch4–5 = reserved (silence), ch6–7 = speaker echo |
| `SubscribeInternalVideoDesensed` | 125 frames / 5 s, **640×480, 30 fps, 614,400 bytes/frame** (YUYV422), faces blurred |
| `SubscribeInternalVideoCapture` | **0 frames** — expected on models with a compute board |
| `PublishExternalAudioPlaybackStream` | 50 frames accepted, echoed back on `SubscribeInternalAudioPlayback` |
| Speaker audible | Microphones went from peak 179 (silence) to 12795 while the tone played |
| `GetVolume()` | **1** on this robot (range 0–200), i.e. nearly muted |

The raw frame is YUYV422 without a container; converting it to a viewable image is a few lines of YUV→RGB. The de-sensed
stream already has faces blurred, which is what 脱敏 means.

## Caveats

- `SubscribeInternalVideoCapture` stays empty on EDU models with a compute board. The official flow is to publish an
  external video stream first; `noetix-video-capture.service` is already doing that, which is why the **de-sensed**
  stream works.
- Playback goes through the robot's voice module, so it is independent of the local `/dev/snd` devices. The compute
  board's own analog input records pure silence: the microphone array is not wired to it.
- `play` raises the volume to 100 for the test and restores the previous value afterwards. Change that constant if the
  tone is too loud or too quiet.
- The voice system may report `Sleeped` (`CmdSleeped`) while the robot is idle; external playback still works.

## Safety

Audio and video only. This sample sends no motion commands and does not stop or restart any robot service.

## 中文

通过 `MediaController` 测试媒体通路（麦克风、扬声器、摄像头）。这里不会让机器人运动：只订阅媒体话题，
播放测试时发一小段音调。

```text
MediaTest [dds.xml 路径] [status|record|play|video|all] [秒数] [输出目录]
```

| 命令 | 作用 |
| --- | --- |
| `status` | 订阅系统状态/错误、麦克风音频和两路视频，打印汇总 |
| `record` | 与 `status` 相同，重点是保存麦克风音频为 WAV |
| `play` | 向播放话题发 1 s 440 Hz 音调，然后报告回传结果 |
| `video` | 与 `status` 相同，重点是摄像头帧 |
| `all` | 采集状态 + 音频 + 视频，然后播放音调 |

录音以 16 bit PCM WAV 保存，两路视频各自的第一帧也写在同一目录，方便在 SDK 之外检查。

## 实测结果（Bumi EDU 算力板，2026-11-19）

| 通路 | 结果 |
| --- | --- |
| `SubscribeInternalAudioCapture` | 5 s 收到 496 帧，**8 通道、16 kHz、format=2**，有真实信号 |
| 通道布局 | ch0–3 = 头顶 4 个麦克风，ch4–5 = 预留（静音），ch6–7 = 扬声器回采 |
| `SubscribeInternalVideoDesensed` | 5 s 收到 125 帧，**640×480、30 fps、614,400 字节/帧**（YUYV422），人脸已打码 |
| `SubscribeInternalVideoCapture` | **0 帧**——带算力板的机型就是这样 |
| `PublishExternalAudioPlaybackStream` | 50 帧被接收，并在 `SubscribeInternalAudioPlayback` 回传 |
| 扬声器确实出声 | 播放时麦克风峰值从 179（静音）升到 12795 |
| `GetVolume()` | 本机为 **1**（范围 0–200），也就是几乎静音 |

原始帧是没有封装头的 YUYV422，转成可看的图片只需几行 YUV→RGB。脱敏流里的人脸已经打码，这就是"脱敏"的含义。

## 注意事项

- 带算力板的 EDU 机型上 `SubscribeInternalVideoCapture` 始终为空。官方流程是先发布外部视频流；
  `noetix-video-capture.service` 已经在做这件事，所以**脱敏**流可用。
- 播放走的是机器人的语音模块，与本地 `/dev/snd` 无关。算力板自己的模拟输入录出来是纯静音——
  麦克风阵列并没有接在算力板上。
- `play` 会在测试期间把音量调到 100，结束后恢复原值。如果音调太响或太轻，改这个常量即可。
- 机器人空闲时语音系统可能上报 `Sleeped`（`CmdSleeped`），外部播放仍然有效。

## 安全

只涉及音频和视频。本示例不发送任何运动指令，也不停止或重启任何机器人服务。
