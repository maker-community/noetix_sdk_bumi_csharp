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
| `playfile <wav> [frame-ms]` | Publishes a 16-bit PCM WAV to the robot speaker in fixed-size frames (default 20 ms), paced in real time |
| `playstdin [rate] [channels] [frame-ms]` | Reads raw 16-bit PCM from stdin and publishes it, so a decoder can stream straight to the robot |
| `video` | Same as `status`, focused on camera frames |
| `loopback <seconds>` | Records the microphone, downmixes the four head mics to stereo, then plays it back |
| `all` | Collects status + audio + video, then plays the tone |
| `volume [n]` | Prints the current volume (0–200); sets it when `n` is given |

The module does **not** decode compressed audio, so decode on the board and pipe it in. `libmpg123` is already
installed on the Bumi compute board (that is what the GStreamer `mpg123audiodec` plugin uses), so an mp3 never has to be
converted on the PC:

```bash
# with the mpg123 CLI (apt-get install mpg123 — needs root)
mpg123 -q -s -r 16000 -m music.mp3 | dotnet MediaTest.dll dds.xml playstdin 16000 1

# without installing anything, using the GStreamer build that ships with the board
gst-launch-1.0 -q filesrc location=music.mp3 ! decodebin ! audioconvert ! audioresample \
  ! audio/x-raw,format=S16LE,rate=16000,channels=1 ! fdsink fd=1 \
  | dotnet MediaTest.dll dds.xml playstdin 16000 1
```

Or convert once with ffmpeg on a PC and use `playfile`:

```bash
ffmpeg -i input.mp3 -ac 1 -ar 16000 -c:a pcm_s16le input-16k-mono.wav
```

### Recommended playback chain

The speaker breaks up on low frequencies, so filter and leave headroom. With `sox` on the board:

```bash
# decode to stereo wav, then high-pass and cut 6 dB
gst-launch-1.0 -q filesrc location=music.mp3 ! decodebin ! audioconvert ! audioresample \
  ! audio/x-raw,format=S16LE,rate=16000,channels=2 ! wavenc ! filesink location=music.wav
sox music.wav music-hp.wav highpass 250 gain -6

dotnet MediaTest.dll dds.xml playfile music-hp.wav media-out 100 120   # 100 ms frames, volume 120
```

Measured on 2026-11-19: mono and unfiltered music were clearly noisy; stereo alone helped, and adding the 250 Hz
high-pass plus -6 dB removed the noise entirely. Frame size (20 ms vs 100 ms) changed nothing. Volume 120 of 200 was
the best of 80 / 120 / 160 / 200.

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

- **If playback is silent, check the phone app first.** Turning the voice assistant off in the app also silences
  `publish_external_audio_playback_stream`; the topic still accepts frames and echoes them back, so the SDK looks
  healthy while nothing is audible. Confirm sound by watching the microphone peak (ambient ≈ 95, playing ≈ 15,000).
- **Playback noise had two causes, both now understood.** Sending **mono** is wrong — the playback topic is specified
  as 2 channels — and low-frequency music makes the small speaker break up. With stereo, a 250 Hz high-pass and about
  -6 dB of headroom the same music is clean on the robot. Frame size (20 ms vs 100 ms) made no measurable difference,
  so per-frame publishing jitter is not a factor. Playback tests should use the chain below.
- **Tone is limited by the speaker, not by the client.** Measured at volume 120: 44.1 kHz sounded *worse* than 16 kHz
  (the module resamples internally), and a low-mid/treble EQ (+2 dB at 300 Hz, +4 dB at 3 kHz) plus lowering the
  high-pass from 250 Hz to 130 Hz were both below the audibility threshold. The volume sweet spot is about **120 of
  200**; above that the small speaker distorts and below it the result is thin. 16 kHz is a hard ceiling, so there is
  no content above 8 kHz to work with.
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
| `playfile <wav> [帧长ms]` | 把 16 bit PCM WAV 按固定帧长（默认 20 ms）、按实时节奏发给扬声器 |
| `playstdin [采样率] [声道] [帧长ms]` | 从标准输入读原始 16 bit PCM 并发布，方便解码器直接流式送入 |
| `video` | 与 `status` 相同，重点是摄像头帧 |
| `loopback <秒数>` | 录一段麦克风，把 4 个头顶麦混成立体声后回放 |
| `all` | 采集状态 + 音频 + 视频，然后播放音调 |
| `volume [n]` | 打印当前音量（0–200）；给了 `n` 就设置 |

语音模块**不会解码压缩音频**，所以在板子上解码后直接喂进来即可。Bumi 算力板上**已经装了 `libmpg123`**
（GStreamer 的 `mpg123audiodec` 插件就是用它），因此 mp3 完全不用先在 PC 上转换：

```bash
# 有 mpg123 命令行时（apt-get install mpg123 —— 需要 root）
mpg123 -q -s -r 16000 -m music.mp3 | dotnet MediaTest.dll dds.xml playstdin 16000 1

# 不装任何东西，用板子自带的 GStreamer
gst-launch-1.0 -q filesrc location=music.mp3 ! decodebin ! audioconvert ! audioresample \
  ! audio/x-raw,format=S16LE,rate=16000,channels=1 ! fdsink fd=1 \
  | dotnet MediaTest.dll dds.xml playstdin 16000 1
```

也可以先在 PC 上用 ffmpeg 转好，再用 `playfile`：

```bash
ffmpeg -i input.mp3 -ac 1 -ar 16000 -c:a pcm_s16le input-16k-mono.wav
```

### 推荐的播放链路

机身喇叭对低频支撑不住，必须滤掉并留余量。板子上有 `sox`：

```bash
# 先解码成立体声 wav，再高通并降 6 dB
gst-launch-1.0 -q filesrc location=music.mp3 ! decodebin ! audioconvert ! audioresample \
  ! audio/x-raw,format=S16LE,rate=16000,channels=2 ! wavenc ! filesink location=music.wav
sox music.wav music-hp.wav highpass 250 gain -6

dotnet MediaTest.dll dds.xml playfile music-hp.wav media-out 100   # 100 ms 帧
```

2026-11-19 实测：单声道和未滤波的音乐明显有杂音；仅改立体声有改善，再加 250 Hz 高通和 -6 dB 后完全干净。
帧长（20 ms 与 100 ms）没有任何差别。音量 80/120/160/200 里 **120** 最好。

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

- **播放没声音时，先检查手机 App。** App 里关掉语音助手后，`publish_external_audio_playback_stream` 也不会有声音；
  但话题照样接收并回传帧，所以 SDK 看起来一切正常。判断是否真的出声，看麦克风回采峰值（环境约 95，播放时约 15,000）。
- **播放杂音有两个原因，现在都清楚了。** 一是发送**单声道**——播放话题规定是 **2 声道**；二是音乐的低频让机身小喇叭破音。
  改用立体声，并加 250 Hz 高通和约 -6 dB 余量后，同一首音乐在机器人上是干净的。帧长（20 ms 与 100 ms）
  实测没有差别，所以逐帧发布抖动不是原因。播放测试请用下面的链路。
- **音色受限于扬声器，不在客户端。** 音量 120 下实测：44.1 kHz **比** 16 kHz 更差（模块内部会重采样）；
  中低频/高频 EQ（300 Hz +2 dB、3 kHz +4 dB）以及把高通从 250 Hz 降到 130 Hz，**都在可听阈值以下**。
  音量甜点约为 **200 中的 120**：再高小喇叭就破音，再低就发虚。16 kHz 是硬上限，8 kHz 以上没有任何内容。
- 带算力板的 EDU 机型上 `SubscribeInternalVideoCapture` 始终为空。官方流程是先发布外部视频流；
  `noetix-video-capture.service` 已经在做这件事，所以**脱敏**流可用。
- 播放走的是机器人的语音模块，与本地 `/dev/snd` 无关。算力板自己的模拟输入录出来是纯静音——
  麦克风阵列并没有接在算力板上。
- `play` 会在测试期间把音量调到 100，结束后恢复原值。如果音调太响或太轻，改这个常量即可。
- 机器人空闲时语音系统可能上报 `Sleeped`（`CmdSleeped`），外部播放仍然有效。

## 安全

只涉及音频和视频。本示例不发送任何运动指令，也不停止或重启任何机器人服务。
