# 真机高层动作验证（挥手） / High-level motion verification on the real robot (waving)

[中文](#中文) | [english](#english)

## 中文

本文记录 **C# 封装在 Bumi EDU 真机上的高层动作实测**，并给出可复现的步骤。
对应示例：[`samples/Noetix.Sdk.Bumi.MotionTest`](../samples/Noetix.Sdk.Bumi.MotionTest/README.md)。
只读状态测试见 [arm64-board-test.md](arm64-board-test.md)。

标注约定：**[官方]** 表示 Noetix 文档/源码明确说明；**[实测]** 表示本仓库在实际机器人上的观察；
**[推断]** 表示尚未验证的推测。

### 结论速览

- **[实测] 2026-11-19，Bumi EDU 算力板（aarch64）+ C# 封装，三个预设动作在真机上全部执行成功**：
  - `Swing`（挥手）：`workmode` 2 → 8 → 2，手臂最大 1.773 rad，**踝关节只有 0.373 rad**。
  - `Shake`（握手）：`workmode` 2 → 9 → 2，手臂最大 1.324 rad，踝关节 0.591 rad。
  - `Cheer`（欢呼）：`workmode` 2 → 10 → 2，手臂最大 1.971 rad，踝关节 ≤0.621 rad。
  - **想"只动手、少动腿"就选 `Swing`**；三个动作都会自行退回走路模式，无需收尾（对比见 4.5）。
- **[实测] 关键前提：`PublishCmd` 必须持续发布。** 发送一次动作后如果停止调用（哪怕只停几秒），机器人不会执行该动作。
  之前的两次尝试就是因为这个原因失败（见第 4 节）。
- **[实测] `Switch` 不是预定动作执行的前置条件。** 从走路模式（2）直接发 `Swing` / `Cheer` 就能触发；
  单独发 `Switch` 在本机上没有改变 `workmode`。

### 1. 官方语义（[官方]）

`HighController::publish_cmd(x, y, z, action, index)`：

| 参数 | 含义 |
| --- | --- |
| `x` | 前后，`x>0` 前进，范围 [-1, 1] |
| `y` | 左右，`y>0` 左移，范围 [-1, 1] |
| `z` | 转向，`z>0` 左转，范围 [-1, 1] |
| `action` | 对应 `workmode` 的策略切换/动作触发 |
| `index` | 仅示教相关模式使用（示教文件索引） |

- **[官方] 官方示例是死循环持续调用**：Python 示例 `sleep(0.01)`（约 100 Hz），C++ 示例 `sleep_ms(2)`（约 500 Hz）；
  空闲时发 `ControlCmd.DEFAULT`。官方备注要求两次调用间隔至少 2 ms。
- **[官方] 动作是瞬发的**：示例用 `key_inuse` 保证每个动作只发一次，下一帧立刻回到 `DEFAULT`。
- **[官方] `(x, y, z)` 行进数据只在特定模式下有效**（走路、打招呼、握手、欢呼、示教等）。
- **[官方] `workmode` 只在 HighController 模式下有效**，取值：

| `workmode` | 含义 | `workmode` | 含义 |
| --- | --- | --- | --- |
| 0 | 使能模式 | 23 | 播放示教 |
| 1 | 准备模式 | 26 | 保护模式 |
| 2 | 走路模式 | 27 | 倒地起身 |
| 5 | 舞蹈模式 | 28 | 起身倒地 |
| 8 | 出厂预设动作 打招呼（挥手） | 29 | 保存示教2 |
| 9 | 出厂预设动作 握手 | 30 | 失能模式 |
| 10 | 出厂预设动作 欢呼 | 31 | 舞蹈模式1 |
| 11 | 开始示教 | 32 | 舞蹈模式2 |
| 12 | 结束示教 | 33 | 擦眼泪动作 |
| 14 | 保存示教1 | | |

- **[官方] 常用 `ControlCmd` 与 workmode 的对应**：`WALK(0)`→走路模式 2，`SWING(1)`→挥手 8，`SHAKE(2)`→握手 9，
  `CHEER(3)`→欢呼 10，`START(5)`=使能/失能开关，`SWITCH(6)`=准备模式 1，`RUN(4)` 预留不可用。
- **[官方] `START` 的行为**：`workmode == 30` 时使机器人**使能**；`workmode != 30` 时使机器人**立刻失能**。
  所以在 `workmode` 2 下误发 `START` 会让机器人失能，而不是"启用"。
- **[官方] 保护模式（26）优先级最高**：IMU 姿态超出站立稳定区间时，运控会强制切到安全模式——全身关节失能、
  终止当前策略、**阻断后续动作指令**，且不受上层指令影响。

#### 1.1 关节与电机 id 对照（[官方]）

`RobotHardwareStatus.motor_data[i]` 的下标即关节 id，来自官方"关节电机信息"表：

| id | 关节 | id | 关节 |
| --- | --- | --- | --- |
| 0 | arm_l1 左臂肩关节 pitch | 11 | arm_r2 右臂肩关节 roll |
| 1 | arm_l2 左臂肩关节 roll | 12 | arm_r3 右臂关节 yaw |
| 2 | arm_l3 左臂关节 yaw | 13 | arm_r4 右臂肘关节 pitch |
| 3 | arm_l4 左臂肘关节 pitch | 14 | leg_r1 右腿髋关节 pitch |
| 4 | leg_l1 左腿髋关节 pitch | 15 | leg_r2 右腿髋关节 roll |
| 5 | leg_l2 左腿髋关节 roll | 16 | leg_r3 右大腿关节 yaw |
| 6 | leg_l3 左大腿关节 yaw | 17 | leg_r4 右腿膝关节 pitch |
| 7 | leg_l4 左腿膝关节 pitch | 18 | leg_r5 右腿踝关节 pitch |
| 8 | leg_l5 左腿踝关节 pitch | 19 | leg_r6 右腿踝关节 roll |
| 9 | leg_l6 左腿踝关节 roll | 20 | waist_1 腰关节 yaw |
| 10 | arm_r1 右臂肩关节 pitch | | |

即：**0～3 左臂，4～9 左腿，10～13 右臂，14～19 右腿，20 腰**。`MotionTest` 直接按这张表打印关节名，
并汇总 `arm / hip+knee / foot(踝) / waist` 四个分区的最大位移，用来判断某个动作"腿动了多少"。

### 2. 环境

| 项 | 值 |
| --- | --- |
| 机器人 | Bumi EDU（17 kg，21 自由度） |
| 运控板 | 192.168.55.102（**不对用户开放，不要访问**） |
| 算力板 | 192.168.55.101，Ubuntu 22.04 / aarch64 / Jetson Orin |
| .NET | .NET 10 runtime（`~/.dotnet/dotnet`） |
| 原生库 | `libnoetix_bumi_c.so`（GCC 11.4 编译，官方 SDK 3.1.0 `95b2645`） |
| DDS 配置 | 官方 SDK 自带的 `config/dds.xml`（`AllowMulticast=false`，单播 peer `192.168.55.102`） |

原生库与 DDS 库的部署方式见 [arm64-board-test.md](arm64-board-test.md) 第 1～2 节。本文假设：

```bash
root="$HOME/noetix-csharp-arm64-build-20261118"
export LD_LIBRARY_PATH="$root/build:$root/official-sdk/dds/aarch64/lib"
config="$root/official-sdk/config/dds.xml"
```

### 3. 复现步骤

#### 3.1 在开发机（Windows + .NET 10 SDK）发布

在仓库根目录：

```powershell
dotnet publish .\samples\Noetix.Sdk.Bumi.MotionTest\Noetix.Sdk.Bumi.MotionTest.csproj `
  -c Release -r linux-arm64 --self-contained false `
  -o .\samples\Noetix.Sdk.Bumi.MotionTest\bin\Release\board-motion
scp -P 2222 -r .\samples\Noetix.Sdk.Bumi.MotionTest\bin\Release\board-motion\* `
  noetix@COMPUTE_BOARD_IP:~/board-motion/
```

#### 3.2 先做只读探测（不发任何指令）

```bash
"$HOME/.dotnet/dotnet" "$HOME/board-motion/Noetix.Sdk.Bumi.MotionTest.dll" "$config" probe 5
```

`probe` 模式**不会调用 `PublishCmd`**。期望看到：

```text
Native SDK version: 3.1.0
Probe only: no commands will be published. Duration: 5s
Work modes observed: 2
Final workmode=2, motors_with_error=13, mean|tau|=0.98
Published 0 commands (probe only).
Received 3508 status messages.
```

- [实测] `workmode=2` 说明机器人在走路模式、已使能，可以直接发动作。
- [实测] `motors_with_error=13` 是 `MotorState.error == 1`；官方故障码表只有 `02H`～`0EH`，`1` 不是文档里的故障码，
  含义未知。目前判断它不影响动作执行（挥手成功时同样是 13）。
- 若 `Received 0 status messages`，先解决连通性，不要继续。

#### 3.3 挥手

```bash
"$HOME/.dotnet/dotnet" "$HOME/board-motion/Noetix.Sdk.Bumi.MotionTest.dll" "$config" "Switch>Swing" 12 500
```

参数顺序：`[dds.xml] [动作或动作链] [秒数] [频率Hz]`。

| 参数 | 说明 |
| --- | --- |
| `probe` | 只读，不发布 |
| `Swing` | 单个动作，只在指令流里发一次 |
| `Switch>Swing` | 顺序动作，间隔 4 s，便于观察每次 `workmode` 变化 |
| 秒数 | 整个发布窗口，需大于 `1 s（预流）+ 4 s × 动作数` |
| 频率 | 默认 500 Hz（与官方 C++ 示例一致）；上限 500，因为 SDK 要求间隔 ≥ 2 ms |

#### 3.4 期望输出

```text
Native SDK version: 3.1.0
Actions: Switch -> Swing (each sent once); streaming Default otherwise at 500 Hz; duration: 12s
Baseline 'initial' after 27 status messages; workmode=2.
[  1.0s] sent Switch once; workmode before=2, after=2
[  5.0s] sent Swing once; workmode before=2, after=2
Work modes observed: 2 -> 8
Final workmode=8, motors_with_error=13, mean|tau|=0.81
Published 2 action command(s) and 2975 Default commands.
Received 6068 status messages.
  motor[11] id=11 moved 1.712 rad from baseline
  motor[13] id=13 moved 1.535 rad from baseline
  motor[10] id=10 moved 0.927 rad from baseline
  ...
21 joint(s) moved more than 0.02 rad after the actions were sent.
```

判定标准：**`Work modes observed` 出现 `8`** 且**有若干关节位移大于 0.02 rad**，才算动作真的执行了。
只看"命令发送成功"不算验证——之前的失败案例命令也是发送成功的。

#### 3.5 一次性脚本

把下面内容存成 `~/wave.sh`，`chmod +x ~/wave.sh` 后执行，即可按顺序完成"只读确认 → 挥手"：

```bash
#!/bin/bash
set -u
root="$HOME/noetix-csharp-arm64-build-20261118"
export LD_LIBRARY_PATH="$root/build:$root/official-sdk/dds/aarch64/lib"
app="$HOME/board-motion/Noetix.Sdk.Bumi.MotionTest.dll"
config="$root/official-sdk/config/dds.xml"

"$HOME/.dotnet/dotnet" "$app" "$config" probe 5                  # 只读：确认 workmode 与电机状态
"$HOME/.dotnet/dotnet" "$app" "$config" "Switch>Swing" 12 500    # 挥手
```

### 4. 实测记录

#### 4.1 失败的两次（2026-11-19，旧版示例）

| 起始 `workmode` | 发送内容 | 结果 |
| --- | --- | --- |
| 30（失能） | `Swing` 一次 + `Default` 100 Hz，共 1,000 次指令 / 12 s | 无关节移动，`workmode` 停在 30；收到 6,739 条状态 |
| 2（走路） | `Switch`、`Swing` 各一次 + `Default` 100 Hz，共 1,166 次指令 / 20 s | 无关节移动，`workmode` 停在 2；收到 10,503 条状态 |

当时的判断是"运控不理这些指令"，但真正的原因是**示例在发完动作后停止发布了**：
旧代码在两个动作之间会空转等待最长 6 s，期间一次 `PublishCmd` 都没发。
官方示例从不停止发布，这与我们的用法完全不同。

#### 4.2 成功的一次（2026-11-19，连续发布版）

- 命令：`Switch>Swing`，500 Hz，12 s（1 s 预流 + 4 s 间隔 + 尾部 7 s 空闲流）
- 发布量：2 条动作指令 + 2,975 条 `Default`；收到 6,068 条状态
- `workmode`：2 → 8（挥手）
- 关节位移（相对基线，取窗口中最大偏离）：

| 电机 id | 位移 (rad) | 电机 id | 位移 (rad) |
| --- | --- | --- | --- |
| 11 | 1.712 | 12 | 0.488 |
| 13 | 1.535 | 1 | 0.452 |
| 10 | 0.927 | 8 | 0.369 |
| 7 | 0.666 | 3 | 0.357 |
| 2 | 0.657 | 4 | 0.341 |

21 个关节全部超过 0.02 rad。id 10～13 变化最大，[推断] 对应对称手臂关节。
`mean|tau|` 0.81 N·m（悬挂状态，与 0.98 的静止读数同量级）。

#### 4.3 欢呼（Cheer）实测（2026-11-19）

- 命令：`Cheer`，500 Hz，12 s（只发一个动作，不需要 `Switch`）
- 发布量：1 条动作指令 + 2,987 条 `Default`；收到 6,067 条状态
- `workmode`：**2 → 10 → 2**（进入欢呼、执行完后自行回到走路模式）
- 关节位移（相对基线）：

| 电机 id | 位移 (rad) | 电机 id | 位移 (rad) |
| --- | --- | --- | --- |
| 13 | 1.971 | 12 | 0.906 |
| 3 | 1.877 | 7 | 0.814 |
| 1 | 1.760 | 2 | 0.621 |
| 11 | 1.735 | | |
| 0 | 1.608 | | |
| 10 | 1.275 | | |
| 17 | 0.967 | | |

21 个关节全部超过 0.02 rad，动作幅度明显大于挥手（最大 1.971 vs 1.712 rad）。

`2 → 10 → 2` 这组序列比挥手那次更有说服力：挥手测试时窗口结束时机器人还停在 8，
而欢呼是**在同一个 12 s 窗口内完成并自行回到 2**，说明预设动作是"执行完自动退出"的，
之前 8 只是窗口恰好截断在动作中途。

#### 4.4 握手（Shake）实测（2026-11-19）

- 命令：`Shake`，500 Hz，12 s → `workmode`：**2 → 9 → 2**（握手，执行完自行回到走路模式）
- 发布量：1 条动作指令 + 2,989 条 `Default`；收到 6,082 条状态
- 关节位移：`arm_l3` 举手 yaw 1.392 rad、`arm_r1` 右肩 pitch 1.276 rad、`arm_l4` 左肘 1.180 rad，
  腿部最大 `leg_l1` 左髋 0.945 rad

#### 4.5 三个预设动作的"腿部参与度"对比

同一个 `probe`/`Swing`/`Shake` 构建（输出带官方关节名与分区统计），500 Hz：

| 动作 | `workmode` 序列 | 手臂最大 | 髋/膝最大 | **踝（脚步）最大** | 腰最大 |
| --- | --- | --- | --- | --- | --- |
| `Swing`（挥手） | 2 → 8 → 2 | 1.773 rad | 0.954 rad | **0.373 rad** | 0.081 rad |
| `Shake`（握手） | 2 → 9 → 2 | 1.324 rad | 0.916 rad | 0.591 rad | 0.128 rad |
| `Cheer`（欢呼） | 2 → 10 → 2 | 1.971 rad | ≥0.967 rad | ≤0.621 rad | ≤0.621 rad |

> `Cheer` 那一行是用未拆分踝关节的旧构建测的，踝/腰按已知的最大值上界给出。

结论：**[实测] 三个预设动作里腿部动作最少的是 `Swing`（挥手）**——踝关节 0.373 rad（约 21°），
腰几乎不动（0.081 rad），手臂 1.773 rad。握手次之，欢呼最大。

但要注意：**没有任何预设动作是"只动手"的**。运控是黑盒的全身平衡策略，发动作时腿一定会参与维持平衡，
腿部 0.9 rad 量级的髋/膝变化在三个动作里都存在。如果确实需要上身动作、腿部完全不动，
只能走**示教模式**：`StartTeach` 手动摆姿势 → `SaveTeach` → `EndTeach` → `PlayTeach` 回放
（见 6.6，示教时需要人力掰关节，且回放时运控仍会做平衡补偿）。

#### 4.6 测试后的状态

- **[实测]** 三次动作结束后再次只读探测：`workmode` 都回到 2（走路模式），`mean|tau|` 0.92～1.16 N·m，
  状态正常，不需要额外收尾动作。
- **[实测]** `Switch` 在本机上没有从 2 切到 1；`workmode` 表里 1=准备模式、`SWITCH(6)`=准备模式，
  但实际观察是 2→2。原因未查明，不影响 `Swing` / `Shake` / `Cheer` 的执行。

### 5. 排错对照

| 现象 | 可能原因 | 处理 |
| --- | --- | --- |
| 退出码 4，`workmode=30` 或 `26` | 机器人失能或在保护模式 | 30：先发 `Start` 使能；26：检查姿态/是否有异物卡住，重上电后重试 |
| 退出码 2，`Received 0 status messages` | DDS 不通 | 检查 `LD_LIBRARY_PATH`、`config/dds.xml`、算力板是否 `192.168.55.101` |
| `DllNotFoundException` | 原生库或 CycloneDDS 库缺失 | `ldd libnoetix_bumi_c.so` 确认 `not found` 为空 |
| 命令都成功、`workmode` 不变、关节不动 | **发布不连续**（最常见） | 用本示例：整段窗口持续发布，动作夹在流里 |
| `workmode` 变 8 但关节几乎不动 | 机器人被外力限制或已接近目标姿态 | 确认安全带没有拉住手臂，重新运行 |
| `HighController.Init()` 长时间不返回 | 无机器人可达 | 用 `timeout` 包裹；不要放在 UI 线程 |
| 退出码 5 | 窗口太短，动作没发完 | 加大秒数：`1 + 4 × 动作数` 以上 |

### 6. 后续测试计划

按风险从低到高推进，每一步都要先 `probe` 确认起始 `workmode`，并记录**动作前后的 `workmode` 序列**与关节位移。
不要为了"看起来更快"缩短发布间隔——SDK 要求 ≥ 2 ms。

#### 6.1 预设动作（风险最低，已全部完成）

| 动作 | 期望 `workmode` | 状态 | 踝关节最大位移 |
| --- | --- | --- | --- |
| `Swing`（挥手） | 8 | **[实测] 已通过**（2 → 8 → 2） | 0.373 rad |
| `Shake`（握手） | 9 | **[实测] 已通过**（2 → 9 → 2） | 0.591 rad |
| `Cheer`（欢呼） | 10 | **[实测] 已通过**（2 → 10 → 2） | ≤0.621 rad |

三个预设动作都能执行并自动退回走路模式。**想少动腿就选 `Swing`**（对比见 4.5）。

命令：`"$HOME/.dotnet/dotnet" "$HOME/board-motion/Noetix.Sdk.Bumi.MotionTest.dll" "$config" "Swing" 10 500`。

#### 6.2 模式切换

- 验证 `Switch` 在当前固件上到底会不会改变 `workmode`（本机实测 2→2）。用 `Switch>Switch` 或 `Switch` 单独跑一次，
  记录 `Work modes observed`。
- 验证 `Start` 的行为：从 30 → 使能；从非 30 → **立刻失能**。这条要单独测，避免和动作混在一起。

#### 6.3 舞蹈（场地与安全要求高）

- `Dance`（舞蹈1，`workmode` 5）、`Dance1`（31）、`Dance2`（32）、`Tear`（擦眼泪，33）。
- 官方安全手册要求：场地平坦不打滑、周围无杂物（舞蹈是"盲舞"）；禁止在人多的环境使用。
- 建议先只测 `Dance`，窗口给到 30 s 以上。

#### 6.4 起身/倒地（必须在安全范围内）

- `FALLTOSTAND`（倒地起身，27）、`STANDTOFALL`（起身倒地，28）。
- 官方要求：起身动作波及约 **3 m × 3 m**，范围里不能有杂物；机器人姿态不对时运控会判定摔倒并进保护模式。
- 测这两个动作前，确认安全带不会在起身途中拉住身体。

#### 6.5 行走

- 用 `PublishCmd(x>0, 0, 0, ControlCmd.Default)` 持续发布测试前进，先给很小的值（如 0.1）和短窗口。
- 只有在机器人**站在地面**、安全带松开且有人员看护时才做；**禁止全身行走步态下把机器人吊起来让双脚悬空**
  （官方明令禁止）。
- 需要同时验证：停止发布后机器人是否会自行停下（目前**未验证**）。

#### 6.6 示教

- `StartTeach`（11）→ 手动摆动作 → `SaveTeach`（14，`index` 递增）→ `EndTeach`（12）→ `PlayTeach`（23，`index` 指定文件）。
- 官方说明：`SAVETEACH` 后会依次返回"退出示教 / 保存示教1 / 保存示教2"三个状态。
- `ENDTEACH(9)` 官方标注**已弃用**。

#### 6.7 仍然未知的问题

- `MotorState.error == 1` 的含义（官方故障码表里没有 1）。
- 指令流中断/退出后机器人是否会自行停下。
- 各动作的前置 `workmode` 与厂家流程图的完整对应关系（本机证明 `Swing` / `Cheer` 都不要求先切准备模式）。

### 7. 安全

- 每次发运动指令前：**系好安全带（吊架/安全绳），清理周围环境，手边有急停**。
- 不要在机器人行走步态、双脚悬空时做动作测试（官方明令禁止）。
- 不要访问运控板 `192.168.55.102`，不要改动机器人上正在运行的官方服务。
- 不要为了"多试几次"在两个模式之间快速反复切换：官方明确说物理惯性会导致摔倒。

## english

This document records the **measured high-level actions of the C# wrapper on a real Bumi EDU robot** and gives reproducible steps.
Corresponding sample: [`samples/Noetix.Sdk.Bumi.MotionTest`](../samples/Noetix.Sdk.Bumi.MotionTest/README.md).
Read-only status tests are in [arm64-board-test.md](arm64-board-test.md).

Claim markers: **[official]** means explicitly stated by the Noetix docs/source; **[measured]** means an observation made on the actual robot in this repository;
**[inference]** means a guess that has not been verified yet.

### Summary

- **[measured] 2026-11-19, Bumi EDU compute board (aarch64) + C# wrapper, all three preset actions executed successfully on the real robot**:
  - `Swing` (wave): `workmode` 2 → 8 → 2, maximum arm 1.773 rad, **ankle joint only 0.373 rad**.
  - `Shake` (handshake): `workmode` 2 → 9 → 2, maximum arm 1.324 rad, ankle 0.591 rad.
  - `Cheer` (cheer): `workmode` 2 → 10 → 2, maximum arm 1.971 rad, ankle ≤0.621 rad.
  - **To "move only the arms and barely the legs", choose `Swing`**; all three actions return to walking mode by themselves, no wrap-up needed (comparison in 4.5).
- **[measured] Key prerequisite: `PublishCmd` must keep publishing.** If the call stops after sending an action once (even for only a few seconds), the robot does not execute that action.
  The two earlier attempts failed for exactly this reason (see section 4).
- **[measured] `Switch` is not a prerequisite for executing the preset actions.** Sending `Swing` / `Cheer` directly from walking mode (2) triggers them;
  sending `Switch` alone did not change `workmode` on this robot.

### 1. Official semantics ([official])

`HighController::publish_cmd(x, y, z, action, index)`:

| Parameter | Meaning |
| --- | --- |
| `x` | forward/backward, `x>0` moves forward, range [-1, 1] |
| `y` | left/right, `y>0` moves left, range [-1, 1] |
| `z` | turning, `z>0` turns left, range [-1, 1] |
| `action` | policy switch/action trigger corresponding to `workmode` |
| `index` | used only by teaching-related modes (teaching file index) |

- **[official] The official samples call it continuously in an infinite loop**: the Python sample uses `sleep(0.01)` (about 100 Hz), the C++ sample `sleep_ms(2)` (about 500 Hz);
  `ControlCmd.DEFAULT` is sent when idle. The official notes require at least 2 ms between two calls.
- **[official] Actions are instantaneous**: the samples use `key_inuse` to ensure each action is sent only once and the next frame immediately returns to `DEFAULT`.
- **[official] The `(x, y, z)` travel data is only valid in specific modes** (walking, greeting, handshaking, cheering, teaching, etc.).
- **[official] `workmode` is only valid in HighController mode**, with the values:

| `workmode` | Meaning | `workmode` | Meaning |
| --- | --- | --- | --- |
| 0 | enable mode | 23 | play teaching |
| 1 | prepare mode | 26 | protection mode |
| 2 | walking mode | 27 | fall to stand |
| 5 | dance mode | 28 | stand to fall |
| 8 | factory preset action greeting (wave) | 29 | save teaching 2 |
| 9 | factory preset action handshake | 30 | disabled mode |
| 10 | factory preset action cheer | 31 | dance mode 1 |
| 11 | start teaching | 32 | dance mode 2 |
| 12 | end teaching | 33 | wipe tears action |
| 14 | save teaching 1 | | |

- **[official] Mapping of the common `ControlCmd` values to workmode**: `WALK(0)`→walking mode 2, `SWING(1)`→wave 8, `SHAKE(2)`→handshake 9,
  `CHEER(3)`→cheer 10, `START(5)`=enable/disable switch, `SWITCH(6)`=prepare mode 1, `RUN(4)` is reserved and unavailable.
- **[official] Behaviour of `START`**: when `workmode == 30` it **enables** the robot; when `workmode != 30` it **immediately disables** the robot.
  So sending `START` by mistake while in `workmode` 2 disables the robot instead of "enabling" it.
- **[official] Protection mode (26) has the highest priority**: when the IMU attitude leaves the stable standing range, motion control forcibly switches to the safe mode — all joints are disabled,
  the current policy is terminated, **subsequent action commands are blocked**, and it is not affected by upper-layer commands.

#### 1.1 Joint and motor id mapping ([official])

The index of `RobotHardwareStatus.motor_data[i]` is the joint id, taken from the official "joint motor information" table:

| id | joint | id | joint |
| --- | --- | --- | --- |
| 0 | arm_l1 left arm shoulder joint pitch | 11 | arm_r2 right arm shoulder joint roll |
| 1 | arm_l2 left arm shoulder joint roll | 12 | arm_r3 right arm joint yaw |
| 2 | arm_l3 left arm joint yaw | 13 | arm_r4 right arm elbow joint pitch |
| 3 | arm_l4 left arm elbow joint pitch | 14 | leg_r1 right leg hip joint pitch |
| 4 | leg_l1 left leg hip joint pitch | 15 | leg_r2 right leg hip joint roll |
| 5 | leg_l2 left leg hip joint roll | 16 | leg_r3 right thigh joint yaw |
| 6 | leg_l3 left thigh joint yaw | 17 | leg_r4 right leg knee joint pitch |
| 7 | leg_l4 left leg knee joint pitch | 18 | leg_r5 right leg ankle joint pitch |
| 8 | leg_l5 left leg ankle joint pitch | 19 | leg_r6 right leg ankle joint roll |
| 9 | leg_l6 left leg ankle joint roll | 20 | waist_1 waist joint yaw |
| 10 | arm_r1 right arm shoulder joint pitch | | |

That is: **0–3 left arm, 4–9 left leg, 10–13 right arm, 14–19 right leg, 20 waist**. `MotionTest` prints the joint names directly from this table,
and summarises the maximum displacement of the four regions `arm / hip+knee / foot(ankle) / waist` to judge "how much the legs moved" for a given action.

### 2. Environment

| Item | Value |
| --- | --- |
| Robot | Bumi EDU (17 kg, 21 degrees of freedom) |
| Motion control board | 192.168.55.102 (**not exposed to users, do not access**) |
| Compute board | 192.168.55.101, Ubuntu 22.04 / aarch64 / Jetson Orin |
| .NET | .NET 10 runtime (`~/.dotnet/dotnet`) |
| Native library | `libnoetix_bumi_c.so` (built with GCC 11.4, official SDK 3.1.0 `95b2645`) |
| DDS configuration | the `config/dds.xml` shipped with the official SDK (`AllowMulticast=false`, unicast peer `192.168.55.102`) |

How the native library and DDS library are deployed is described in sections 1–2 of [arm64-board-test.md](arm64-board-test.md). This document assumes:

```bash
root="$HOME/noetix-csharp-arm64-build-20261118"
export LD_LIBRARY_PATH="$root/build:$root/official-sdk/dds/aarch64/lib"
config="$root/official-sdk/config/dds.xml"
```

### 3. Reproduction steps

#### 3.1 Publish on the development machine (Windows + .NET 10 SDK)

In the repository root:

```powershell
dotnet publish .\samples\Noetix.Sdk.Bumi.MotionTest\Noetix.Sdk.Bumi.MotionTest.csproj `
  -c Release -r linux-arm64 --self-contained false `
  -o .\samples\Noetix.Sdk.Bumi.MotionTest\bin\Release\board-motion
scp -P 2222 -r .\samples\Noetix.Sdk.Bumi.MotionTest\bin\Release\board-motion\* `
  noetix@COMPUTE_BOARD_IP:~/board-motion/
```

#### 3.2 Read-only probe first (publishes no commands)

```bash
"$HOME/.dotnet/dotnet" "$HOME/board-motion/Noetix.Sdk.Bumi.MotionTest.dll" "$config" probe 5
```

`probe` mode **does not call `PublishCmd`**. Expected output:

```text
Native SDK version: 3.1.0
Probe only: no commands will be published. Duration: 5s
Work modes observed: 2
Final workmode=2, motors_with_error=13, mean|tau|=0.98
Published 0 commands (probe only).
Received 3508 status messages.
```

- [measured] `workmode=2` means the robot is in walking mode and enabled, so actions can be sent directly.
- [measured] `motors_with_error=13` is `MotorState.error == 1`; the official fault code table only contains `02H`–`0EH`, `1` is not a fault code in the documentation,
  and its meaning is unknown. The current assessment is that it does not affect action execution (it is also 13 when the wave succeeds).
- If you see `Received 0 status messages`, fix connectivity first and do not continue.

#### 3.3 Waving

```bash
"$HOME/.dotnet/dotnet" "$HOME/board-motion/Noetix.Sdk.Bumi.MotionTest.dll" "$config" "Switch>Swing" 12 500
```

Argument order: `[dds.xml] [action or action chain] [seconds] [frequency Hz]`.

| Argument | Description |
| --- | --- |
| `probe` | read-only, publishes nothing |
| `Swing` | a single action, sent only once in the command stream |
| `Switch>Swing` | sequential actions, 4 s apart, making each `workmode` change easy to observe |
| seconds | the whole publishing window, must be greater than `1 s (pre-stream) + 4 s × number of actions` |
| frequency | 500 Hz by default (the same as the official C++ sample); the upper limit is 500 because the SDK requires an interval ≥ 2 ms |

#### 3.4 Expected output

```text
Native SDK version: 3.1.0
Actions: Switch -> Swing (each sent once); streaming Default otherwise at 500 Hz; duration: 12s
Baseline 'initial' after 27 status messages; workmode=2.
[  1.0s] sent Switch once; workmode before=2, after=2
[  5.0s] sent Swing once; workmode before=2, after=2
Work modes observed: 2 -> 8
Final workmode=8, motors_with_error=13, mean|tau|=0.81
Published 2 action command(s) and 2975 Default commands.
Received 6068 status messages.
  motor[11] id=11 moved 1.712 rad from baseline
  motor[13] id=13 moved 1.535 rad from baseline
  motor[10] id=10 moved 0.927 rad from baseline
  ...
21 joint(s) moved more than 0.02 rad after the actions were sent.
```

Pass criterion: only when **`Work modes observed` contains `8`** and **several joints moved more than 0.02 rad** has the action really been executed.
"Command sent successfully" alone is not verification — the earlier failure cases also sent their commands successfully.

#### 3.5 One-shot script

Save the following as `~/wave.sh`, run `chmod +x ~/wave.sh` and then execute it to perform "read-only confirmation → waving" in order:

```bash
#!/bin/bash
set -u
root="$HOME/noetix-csharp-arm64-build-20261118"
export LD_LIBRARY_PATH="$root/build:$root/official-sdk/dds/aarch64/lib"
app="$HOME/board-motion/Noetix.Sdk.Bumi.MotionTest.dll"
config="$root/official-sdk/config/dds.xml"

"$HOME/.dotnet/dotnet" "$app" "$config" probe 5                  # 只读：确认 workmode 与电机状态
"$HOME/.dotnet/dotnet" "$app" "$config" "Switch>Swing" 12 500    # 挥手
```

### 4. Measured records

#### 4.1 The two failures (2026-11-19, older sample)

| Starting `workmode` | What was sent | Result |
| --- | --- | --- |
| 30 (disabled) | `Swing` once + `Default` at 100 Hz, 1,000 commands / 12 s in total | No joint movement, `workmode` stayed at 30; 6,739 status messages received |
| 2 (walking) | `Switch`, `Swing` once each + `Default` at 100 Hz, 1,166 commands / 20 s in total | No joint movement, `workmode` stayed at 2; 10,503 status messages received |

The judgement at the time was "motion control ignores these commands", but the real cause was **that the sample stopped publishing after sending the actions**:
the old code spun in place for up to 6 s between the two actions, during which not a single `PublishCmd` was sent.
The official samples never stop publishing, which is completely different from our usage.

#### 4.2 The successful run (2026-11-19, continuous publishing version)

- Command: `Switch>Swing`, 500 Hz, 12 s (1 s pre-stream + 4 s interval + 7 s idle stream at the end)
- Published: 2 action commands + 2,975 `Default`; 6,068 status messages received
- `workmode`: 2 → 8 (wave)
- Joint displacement (relative to the baseline, taking the maximum deviation within the window):

| motor id | displacement (rad) | motor id | displacement (rad) |
| --- | --- | --- | --- |
| 11 | 1.712 | 12 | 0.488 |
| 13 | 1.535 | 1 | 0.452 |
| 10 | 0.927 | 8 | 0.369 |
| 7 | 0.666 | 3 | 0.357 |
| 2 | 0.657 | 4 | 0.341 |

All 21 joints exceeded 0.02 rad. ids 10–13 changed the most, [inference] corresponding to the symmetric arm joints.
`mean|tau|` 0.81 N·m (hanging state, the same order of magnitude as the 0.98 static reading).

#### 4.3 Cheer measurement (2026-11-19)

- Command: `Cheer`, 500 Hz, 12 s (only one action is sent, `Switch` is not needed)
- Published: 1 action command + 2,987 `Default`; 6,067 status messages received
- `workmode`: **2 → 10 → 2** (enters cheer and returns to walking mode by itself after execution)
- Joint displacement (relative to the baseline):

| motor id | displacement (rad) | motor id | displacement (rad) |
| --- | --- | --- | --- |
| 13 | 1.971 | 12 | 0.906 |
| 3 | 1.877 | 7 | 0.814 |
| 1 | 1.760 | 2 | 0.621 |
| 11 | 1.735 | | |
| 0 | 1.608 | | |
| 10 | 1.275 | | |
| 17 | 0.967 | | |

All 21 joints exceeded 0.02 rad, and the motion amplitude is clearly larger than the wave (maximum 1.971 vs 1.712 rad).

The `2 → 10 → 2` sequence is more convincing than the wave run: during the wave test the robot was still at 8 when the window ended,
whereas cheer **completed and returned to 2 within the same 12 s window**, showing that preset actions "exit automatically when done",
and the earlier 8 only meant the window happened to cut off in the middle of the action.

#### 4.4 Shake measurement (2026-11-19)

- Command: `Shake`, 500 Hz, 12 s → `workmode`: **2 → 9 → 2** (handshake, returns to walking mode by itself after execution)
- Published: 1 action command + 2,989 `Default`; 6,082 status messages received
- Joint displacement: `arm_l3` raising the hand yaw 1.392 rad, `arm_r1` right shoulder pitch 1.276 rad, `arm_l4` left elbow 1.180 rad,
  largest leg value `leg_l1` left hip 0.945 rad

#### 4.5 "Leg involvement" comparison of the three preset actions

The same `probe`/`Swing`/`Shake` build (its output includes the official joint names and per-region statistics), 500 Hz:

| Action | `workmode` sequence | arm max | hip/knee max | **ankle (foot) max** | waist max |
| --- | --- | --- | --- | --- | --- |
| `Swing` (wave) | 2 → 8 → 2 | 1.773 rad | 0.954 rad | **0.373 rad** | 0.081 rad |
| `Shake` (handshake) | 2 → 9 → 2 | 1.324 rad | 0.916 rad | 0.591 rad | 0.128 rad |
| `Cheer` (cheer) | 2 → 10 → 2 | 1.971 rad | ≥0.967 rad | ≤0.621 rad | ≤0.621 rad |

> The `Cheer` row was measured with an older build that did not split out the ankle joints; ankle/waist are given as upper bounds from the known maximum values.

Conclusion: **[measured] of the three preset actions, the one that moves the legs least is `Swing` (wave)** — ankle joint 0.373 rad (about 21°),
the waist barely moves (0.081 rad), the arm 1.773 rad. Handshake is next, cheer is the largest.

But note: **no preset action is "arms only"**. Motion control is a black-box whole-body balance policy, so the legs always take part in maintaining balance when an action is sent,
and hip/knee changes on the order of 0.9 rad exist in all three actions. If an upper-body action with completely still legs is really required,
the only way is **teaching mode**: `StartTeach` to move the pose by hand → `SaveTeach` → `EndTeach` → `PlayTeach` playback
(see 6.6; teaching requires moving the joints by hand, and motion control still performs balance compensation during playback).

#### 4.6 State after testing

- **[measured]** A read-only probe again after the three actions: `workmode` had returned to 2 (walking mode) every time, `mean|tau|` 0.92–1.16 N·m,
  the state was normal, and no additional wrap-up action was needed.
- **[measured]** `Switch` did not switch from 2 to 1 on this robot; the `workmode` table says 1=prepare mode and `SWITCH(6)`=prepare mode,
  but the actual observation was 2→2. The cause has not been determined, and it does not affect execution of `Swing` / `Shake` / `Cheer`.

### 5. Troubleshooting reference

| Symptom | Possible cause | Handling |
| --- | --- | --- |
| Exit code 4, `workmode=30` or `26` | The robot is disabled or in protection mode | 30: send `Start` first to enable; 26: check the attitude / whether something is blocking it, power-cycle and retry |
| Exit code 2, `Received 0 status messages` | DDS is not connected | Check `LD_LIBRARY_PATH`, `config/dds.xml`, and whether the compute board is `192.168.55.101` |
| `DllNotFoundException` | Native library or CycloneDDS library missing | `ldd libnoetix_bumi_c.so` and confirm `not found` is empty |
| All commands succeed, `workmode` unchanged, joints do not move | **Publishing is not continuous** (most common) | Use this sample: publish continuously for the whole window, with the action embedded in the stream |
| `workmode` changes to 8 but the joints barely move | The robot is restrained by an external force or is already close to the target pose | Confirm the safety belt is not holding the arm back, then run again |
| `HighController.Init()` does not return for a long time | No robot is reachable | Wrap it with `timeout`; do not call it on the UI thread |
| Exit code 5 | The window is too short and the action was not sent completely | Increase the seconds: more than `1 + 4 × number of actions` |

### 6. Follow-up test plan

Proceed from low to high risk; every step must first `probe` to confirm the starting `workmode`, and record the **`workmode` sequence before and after the action** together with the joint displacements.
Do not shorten the publishing interval in order to "look faster" — the SDK requires ≥ 2 ms.

#### 6.1 Preset actions (lowest risk, all completed)

| Action | Expected `workmode` | Status | Ankle maximum displacement |
| --- | --- | --- | --- |
| `Swing` (wave) | 8 | **[measured] passed** (2 → 8 → 2) | 0.373 rad |
| `Shake` (handshake) | 9 | **[measured] passed** (2 → 9 → 2) | 0.591 rad |
| `Cheer` (cheer) | 10 | **[measured] passed** (2 → 10 → 2) | ≤0.621 rad |

All three preset actions execute and automatically return to walking mode. **Choose `Swing` to move the legs less** (comparison in 4.5).

Command: `"$HOME/.dotnet/dotnet" "$HOME/board-motion/Noetix.Sdk.Bumi.MotionTest.dll" "$config" "Swing" 10 500`.

#### 6.2 Mode switching

- Verify whether `Switch` actually changes `workmode` on the current firmware (measured 2→2 on this robot). Run `Switch>Switch` or `Switch` alone once,
  and record `Work modes observed`.
- Verify the behaviour of `Start`: from 30 → enable; from anything other than 30 → **immediately disable**. Test this one separately so it is not mixed with actions.

#### 6.3 Dance (high site and safety requirements)

- `Dance` (dance 1, `workmode` 5), `Dance1` (31), `Dance2` (32), `Tear` (wipe tears, 33).
- The official safety manual requires: a flat, non-slippery site with no obstacles around (dancing is "blind dancing"); use in crowded environments is forbidden.
- It is recommended to test only `Dance` first, with a window of 30 s or more.

#### 6.4 Stand up / lie down (must stay within the safety envelope)

- `FALLTOSTAND` (fall to stand, 27), `STANDTOFALL` (stand to fall, 28).
- Official requirement: the get-up motion covers about **3 m × 3 m**, with no obstacles in that range; when the robot attitude is wrong, motion control judges it as a fall and enters protection mode.
- Before testing these two actions, confirm that the safety belt will not hold the body back during the get-up.

#### 6.5 Walking

- Use `PublishCmd(x>0, 0, 0, ControlCmd.Default)` publishing continuously to test walking forward, starting with a very small value (e.g. 0.1) and a short window.
- Only do this when the robot is **standing on the ground**, the safety belt is released and personnel are watching; **it is forbidden to suspend the robot with both feet off the ground in a full-body walking gait**
  (explicitly forbidden by the official documentation).
- Also needs to be verified: whether the robot stops by itself after publishing stops (currently **not verified**).

#### 6.6 Teaching

- `StartTeach` (11) → move the pose by hand → `SaveTeach` (14, `index` incremented) → `EndTeach` (12) → `PlayTeach` (23, `index` selects the file).
- Official note: after `SAVETEACH`, three states are returned in order: "exit teaching / save teaching 1 / save teaching 2".
- `ENDTEACH(9)` is marked **deprecated** in the official documentation.

#### 6.7 Still-unknown questions

- The meaning of `MotorState.error == 1` (there is no 1 in the official fault code table).
- Whether the robot stops by itself after the command stream is interrupted / exits.
- The complete correspondence between the prerequisite `workmode` of each action and the manufacturer's flowchart (this robot proves that `Swing` / `Cheer` do not require switching to prepare mode first).

### 7. Safety

- Before every motion command: **fasten the safety belt (gantry/safety rope), clear the surrounding area, and keep the emergency stop at hand**.
- Do not test actions while the robot is in a walking gait with both feet off the ground (explicitly forbidden by the official documentation).
- Do not access the motion control board `192.168.55.102`, and do not modify the official services running on the robot.
- Do not rapidly switch back and forth between two modes in order to "try a few more times": the official documentation explicitly says physical inertia will cause a fall.
