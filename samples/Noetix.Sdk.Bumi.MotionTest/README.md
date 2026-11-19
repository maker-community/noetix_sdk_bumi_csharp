# MotionTest

[english](#english) | [中文](#中文)

## english

Sends high-level `ControlCmd` actions and reports whether the robot actually executed them. It complements
[StatusMonitor](../Noetix.Sdk.Bumi.StatusMonitor/Program.cs), which is read-only.

```text
MotionTest [path-to-dds.xml] [probe|action|Action1>Action2] [seconds] [hz]
```

The sample initializes `HighController`, subscribes to hardware status, and then **streams `PublishCmd`
continuously** — `ControlCmd.Default` by default, with the requested action samples placed inside that stream. This
mirrors the official examples (`examples/cpp/example_high.cpp` publishes every 2 ms, `examples/py/test_high.py` every
10 ms); the SDK asks for at least 2 ms between calls. It prints the observed `workmode` sequence, every joint's
largest position deviation with its official joint name, and a per-region summary (`arm / hip+knee / foot / waist`).

Defaults: the action is `Swing` (挥手), the window is 10 s and the rate is 500 Hz. Use `probe` to read state without
publishing anything — that mode never calls `PublishCmd`.

### Do not go silent

An earlier version sent the action once and then stopped publishing while it waited for the mode to change. Nothing
moved that way, and later verification showed the robot also ignored a `Switch>Swing` sequence sent the same way.
Commands have to arrive as an uninterrupted stream, exactly as the vendor examples do. Keeping the stream running is
what makes the preset actions execute.

### Robot state matters

Preset actions only run when the robot is enabled. Per the official `workmode` table (Bumi document center):

| `workmode` | Meaning |
| --- | --- |
| 0 | 使能模式 enabled |
| 1 | 准备模式 ready |
| 2 | 走路模式 walking |
| 5 | 舞蹈模式 |
| 8 / 9 / 10 | 出厂预设动作 打招呼 / 握手 / 欢呼 (wave / handshake / cheer) |
| 26 | 保护模式 protection |
| 30 | 失能模式 disabled |

`SWITCH` is 准备模式, `SWING` is 挥手, and `START` toggles enable/disable: from 30 it enables, from any other mode it
disables immediately. The sample refuses to publish when it sees 26 or 30 (exit `4`); send `Start` first in that case.
Protection mode has the highest priority in the robot's controller and blocks further action commands.

### Verified sequence

All three presets were measured on the robot, starting from `workmode` 2 and returning to 2 by themselves at 500 Hz:

| Action | `workmode` | Largest arm | Largest ankle (foot) |
| --- | --- | --- | --- |
| `Swing` (挥手) | 2 → 8 → 2 | 1.773 rad | **0.373 rad** |
| `Shake` (握手) | 2 → 9 → 2 | 1.324 rad | 0.591 rad |
| `Cheer` (欢呼) | 2 → 10 → 2 | 1.971 rad | ≤0.621 rad |

`Swing` is the preset that moves the legs least, but none of them is arm-only — the balance controller always moves
the hips and knees (≈0.9 rad). `Switch` alone did not change the mode; the preset action is what took effect. See
[motion-test.md](../../docs/motion-test.md) for the full record, the 21-joint id map and step-by-step reproduction.

### Reading the output

The sample prints the official joint name for each id and a per-region summary:

```text
Max |dpos| by region: arm=1.773 rad, hip/knee=0.954 rad, foot=0.373 rad, waist=0.081 rad
  [13] arm_r4 elbow pitch     moved 1.524 rad from baseline
```

Use the `foot` figure when you want an action that barely moves the legs.

### Safety

Run only with the robot wearing its safety harness and the surrounding area cleared. Do not run this against a robot
in a walking gait while its feet are off the ground — the official safety manual calls that out explicitly.

## 中文

发送高层 `ControlCmd` 动作，并报告机器人是否真的执行了。它与只读的
[StatusMonitor](../Noetix.Sdk.Bumi.StatusMonitor/Program.cs) 互补。

```text
MotionTest [dds.xml 路径] [probe|动作|动作1>动作2] [秒数] [Hz]
```

示例初始化 `HighController`、订阅硬件状态，然后**持续发布 `PublishCmd`**——默认发 `ControlCmd.Default`，
把请求的动作样本夹在这条流里。这与官方示例一致（`examples/cpp/example_high.cpp` 每 2 ms 发一次，
`examples/py/test_high.py` 每 10 ms 一次）；SDK 要求两次调用间隔至少 2 ms。程序会打印观察到的 `workmode` 序列、
每个关节的最大位移（带官方关节名），以及按分区汇总的 `手臂 / 髋膝 / 脚踝 / 腰` 数据。

默认：动作为 `Swing`（挥手），窗口 10 s，频率 500 Hz。用 `probe` 可以只读状态、不发布任何指令——
该模式从不调用 `PublishCmd`。

### 不要中断发布

早期版本发一次动作后就停止发布、静等模式变化，结果什么都不动；后续验证还发现，用同样的方式发
`Switch>Swing` 机器人同样不理。指令必须是连续不断的流，与厂商示例完全一致。**保持流不断，预设动作才会执行。**

### 机器人状态很重要

预设动作只在机器人使能时执行。按官方 `workmode` 表（Bumi 文档中心）：

| `workmode` | 含义 |
| --- | --- |
| 0 | 使能模式 |
| 1 | 准备模式 |
| 2 | 走路模式 |
| 5 | 舞蹈模式 |
| 8 / 9 / 10 | 出厂预设动作 打招呼 / 握手 / 欢呼 |
| 26 | 保护模式 |
| 30 | 失能模式 |

`SWITCH` 是准备模式，`SWING` 是挥手，`START` 是使能/失能开关：`workmode` 为 30 时使能，其它模式下会立刻失能。
示例在读到 26 或 30 时拒绝发布（退出码 `4`），这种情况先发 `Start`。保护模式在运控里优先级最高，会阻断后续动作指令。

### 已验证的动作

三个预设动作都在真机上实测过，全部从 `workmode` 2 开始、以 500 Hz 发布并自行回到 2：

| 动作 | `workmode` | 手臂最大 | 踝（脚部）最大 |
| --- | --- | --- | --- |
| `Swing`（挥手） | 2 → 8 → 2 | 1.773 rad | **0.373 rad** |
| `Shake`（握手） | 2 → 9 → 2 | 1.324 rad | 0.591 rad |
| `Cheer`（欢呼） | 2 → 10 → 2 | 1.971 rad | ≤0.621 rad |

`Swing` 是腿部动作最少的预设动作，但**没有一个预设是纯手部动作**——平衡策略总会带动髋和膝（约 0.9 rad）。
单独发 `Switch` 没有改变模式，真正生效的是预设动作本身。完整记录、21 关节 id 映射和复现步骤见
[motion-test.md](../../docs/motion-test.md)。

### 如何读输出

示例会打印每个 id 的官方关节名和分区汇总：

```text
Max |dpos| by region: arm=1.773 rad, hip/knee=0.954 rad, foot=0.373 rad, waist=0.081 rad
  [13] arm_r4 elbow pitch     moved 1.524 rad from baseline
```

如果你想要"几乎不动腿"的动作，看 `foot` 这一项。

### 安全

只在机器人系好安全带、周围环境已清理的情况下运行。不要在机器人处于行走步态、双脚悬空时运行——
官方安全手册明确禁止这一点。