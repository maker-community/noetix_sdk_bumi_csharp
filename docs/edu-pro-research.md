# Bumi EDU-Pro 二次开发调研

> 对象：松延动力 Bumi EDU-Pro（带 Jetson Orin Nano Super 算力板）
> 依据：官方[综合使用文档](https://web.noetixrobotics.com/docs/)、官方 SDK 仓库与第三方公开项目，以及对 SDK 源码的阅读。
> 本文不含任何账号、密码或个人信息。**所有"可行"均未经实机验证**，产品、SDK 和文档更新很快，动手前请重新核对官方文档。
> 标记约定：**[官方]** 官方文档/源码明确写了；**[第三方]** 他人项目做过；**[推断]** 我的判断，需要实测。

---

## 1. 结论速览

| 问题 | 结论 |
| --- | --- |
| 带不带算力板？ | **[官方]** 带。EDU-Pro 为 6 + 67 TOPS，模组是 Jetson Orin Nano Super。 |
| 要不要外接 Linux 板？ | 不需要。用户程序部署在算力板上。外挂开发是官方为**无算力板机型**准备的方案（见 3.2）。 |
| 官方推荐的控制方式？ | 应用开发用 **HighController**（调度原厂步态）；仅 EDU 版可用 **LowController**（500 Hz 电机级控制，风险高）。 |
| 语音？ | **[官方]** `MediaController` 提供音频流、扬声器、唤醒词、音量等；机器人头顶 **4 个麦克风**。 |
| 手柄/遥控？ | **[官方]** 官方手柄是"澳加狮"，USB 接收器插在算力板上，由算力板直接读取。 |
| 相机？ | **[官方]** Intel RealSense D435i（文档写作 D435ICB），接在算力板上，用 librealsense 访问。 |
| 牵手跟随？ | 室内、平坦、有人看护的原型可以研究；**牵手出门不能认为已被官方支持**（见第 9 节）。 |
| 有没有 C#？ | 官方只有 C++ / Python。社区封装见第 11 节。 |

---

## 2. 硬件

### 2.1 配置（EDU 版）

| 项目 | EDU-Air | **EDU-Pro** | EDU-Max |
| --- | --- | --- | --- |
| 算力 | 6 TOPS | **6 + 67 TOPS** | 6 + 117 TOPS |
| 高算力模组 | 无 | **Jetson Orin Nano Super** | Jetson Orin NX |
| 自由度 | 21（单腿 6，单臂 4，腰 1） | 同左 | 同左 |
| 重量 / 身高 | 约 17 kg / 98 cm | 同左 | 同左 |
| 续航 | 2–3 h | 同左 | 同左 |
| 关节通信 | EtherCAT + CAN FD | 同左 | 同左 |

标准版相机为 DC_V93，EDU 版（EDU-Air 除外）为 RealSense D435i。麦克风阵列和扬声器为标配。
参数页的"语音模块 / 上层运控二次开发 / 底层运控二次开发"对 EDU 型号显示同一种图标 **[推断]** 为标配，建议向销售书面确认。

### 2.2 两块计算板

| 板子 | 型号 | IP | 是否对用户开放 |
| --- | --- | --- | --- |
| 运控板 | 瑞芯微 RK3576（6 TOPS） | 192.168.55.102 | **不开放**。官方警告：私自访问导致运控失控，损失与厂家无关。 |
| 二开算力板 | Jetson Orin Nano Super（8 GB 内存 + 128 GB 存储） | 192.168.55.101 | **开放**，用户程序部署在这里。登录账号见官方文档。 |

背部接口：1 个 RJ45（千兆交换机）、2 个 USB 3.0、1 个 HDMI。**带算力板的机型，背部接口是算力板的接口。**

---

## 3. 官方 SDK

- 仓库：<https://github.com/Noetix-Robotics/noetix_sdk_bumi>（BSD-3-Clause，当前版本 `SDK_VERSION` = 3.1.0）。
- 官方组织 [Noetix-Robotics](https://github.com/Noetix-Robotics) 下相关仓库：`noetix_bumi_lab`（Isaac Lab 强化学习）、`noetix_dds_docker`（编译环境 Docker）、`noetix_sdk_boomy`、`noetix_ros2_boomy` 等。
- 通信：CycloneDDS 0.11.0，C/S 架构。算力板上的 DDS 客户端以 500 Hz 向运控板上的运控程序发命令，并持续接收状态。
- **核心库 `libnoetix_sdk.a` 是闭源预编译库**；仓库只开放头文件、DDS 库、示例和 pybind 封装。**没有公开 IDL、topic 名称和 QoS**，官方开发接口只列了 DDS / C++ / Python。
- 环境：Ubuntu 22.04、aarch64 或 x86_64、GCC 11.4。
- 官方提供 Docker 编译环境 `noetix_dds.tar.gz`（仅 aarch64）；算力板本身也已装好编译环境。

### 3.1 编译注意（含实测）

- 官方静态库内是 **GCC 11 的 LTO 字节码**：用 GCC 15 链接会失败，需 GCC 11（我在 Ubuntu 22.04 容器验证过）。
- 官方 `HighController` 与 `LowController` **定义了同名全局变量，不能链接进同一个库**（官方 Python 绑定也是分开的模块）。一个进程只用其中一个。
- 官方 FAQ 提到运行 DDS 示例缺 `libssl.so.1.1` 时要手动装 deb 包；我在 Ubuntu 22.04 上链接出来的是 `libssl.so.3`，以算力板实际为准。
- 官方 `CMakeLists.txt` 里 `find_package(onnxruntime REQUIRED)` 是为 `example_policy` 服务的，README 的依赖命令没提；如果编译官方示例报找不到 onnxruntime，需要自行安装 aarch64 版。**C# 封装本身不需要 ONNX Runtime。**
- Python 示例用 `os.environ["CYCLONEDDS_URI"] = "file://config/dds.xml"`，并需要在 SDK 根目录运行。

### 3.2 外挂开发（官方）

官方文档写明：运控板不对外开放，**无算力板的机型**要自备主机，通过网线接背部接口，并且**必须配置成与原版算力板相同的 IP（192.168.55.101）**，因为运控板只允许与该固定 IP 通信。EDU-Pro 自带算力板，不需要这样做。

---

## 4. 运动控制

### 4.1 HighController

保留原厂的平衡与步态，应用只发高层指令；**能力原则上不超过 App 或手柄能做的事**，不开放电机级控制。

| 接口 | 作用 |
| --- | --- |
| `Instance()` / `init()` | 获取单例、初始化 |
| `publish_cmd(x, y, z, action, index)` | 下发命令 |
| `subscribe_robot_hardware_status(callback)` | 订阅状态 |

- `x`、`y`、`z` 范围 [-1, 1]：`x>0` 前进，`y>0` 左移，`z>0` 左转。
- `action`（`ControlCmd`，数值固定）：`WALK=0, SWING=1, SHAKE=2, CHEER=3, RUN=4(预留), START=5, SWITCH=6, STARTTEACH=7, SAVETEACH=8, ENDTEACH=9, PLAYTEACH=10, DANCE=11, FALLTOSTAND=12, STANDTOFALL=13, DANCE1=14, DANCE2=15, TEAR=16, DEFAULT=17`。
- **官方示例按约 100 Hz 持续调用 `publish_cmd`，没有动作时发 `DEFAULT`。** 官方备注：不能连续高速调用，每次至少间隔 2 ms；模式切换须遵循文档流程图。
- 状态回调 `RobotHardwareStatus`：21 个电机（pos/vel/tau/temperature/error）、IMU、手柄、电池（温度/告警/电量/健康度）、`workmode`（仅 HighController 下有效；取值表见官方文档）。
- **回调来自 DDS 内部线程**，应尽快返回。

### 4.2 LowController（仅 EDU）

- 开放电机接口，500 Hz 下发 21 个电机的目标（`MotorCmd.pos` 单位弧度），典型用途是部署强化学习策略。
- 官方提供 21 个关节的扭矩与角度范围，必须遵守。
- **不建议作为入门路径，也不适合用托管语言做硬实时闭环。**

### 4.3 官方安全手册要点

- 禁止雨天、潮湿环境。
- 禁止复杂路面：坑洼、软地面、光滑、楼梯、>20° 斜坡。运控是"盲走"，不能自主感知路面。
- 起身、收纳、舞蹈前，场地须平坦不打滑且周围无杂物（起身波及约 3 m × 3 m）。
- 禁止在人和杂物多的环境使用舞蹈。
- 禁止故意快速反复切换模式；禁止运动中故意干扰机器人。
- **禁止全身行走步态下将机器人拎起使双脚悬空。**
- 禁止私自访问运控板。
- **调试过程中全程使用机器人吊架**（官方示例以 `sudo` 运行）；安装或拆卸吊架前先断电。

---

## 5. 语音与麦克风

### 5.1 MediaController（官方）

| 类别 | 接口 |
| --- | --- |
| 音频流 | `subscribe_internal_audio_capture`、`subscribe_internal_audio_playback` |
| 外部音频 | `publish_external_audio_stream`（给语音程序）、`publish_external_audio_playback_stream`（直接送扬声器） |
| 音量、超时 | `get/set_volume`（**范围 0–200**）、`get/set_timeout` |
| 唤醒词 | `add_wakeup_words`、`reset_wakeup_words`、唤醒/休眠回复词 |
| 数据流开关 | 内部麦克风是否送大模型、外部音频是否送大模型、大模型回复是否输出到扬声器、外部音频是否输出到扬声器、外部音频是否使用内部 3A |
| 控制 | 暂停/恢复音频采集与播放 |
| 视频 | 订阅内部视频流（**仅不带算力板机型**）；带算力板机型需**持续发布外部视频流**给音视频交互系统，才能订阅脱敏视频流 |

### 5.2 麦克风

- **[官方]** `subscribe_internal_audio_capture` 的数据是 **8 通道**：前 4 路是**头顶 4 个麦克风**，第 5、6 路预留，第 7、8 路是**扬声器回采**（可用于回声消除）。
- 拾音距离、角度等指标官方文档**没有写**，需要实测（建议在安静和有噪声环境下，分别于 1/2/3/5 m 录音评估）。
- 采样率、帧长不固定，示例是运行时从数据里读取；发送音频的示例按 **20 ms 一帧**，并且有转双声道交错的处理，`format` 示例中用 `2`，其余取值含义未说明。

### 5.3 已知现象（使用者反馈，原因未明）

- 每次开机音量变得很小；音量设为 200 时破音。
- **[推断]** 音量不持久化；0–200 可能是软件增益，超出某值后数字削波，也可能是扬声器物理上限。
- 区分方法：订阅 `subscribe_internal_audio_playback` 录下实际播放的音频，波形顶部被削平则为数字削波。
- 建议：程序启动时主动 `set_volume`，并找出不失真的最大值作为上限；也可在自己的输出上做限幅。

### 5.4 推荐结构 [推断]

```text
麦克风(MediaController) → 取 4 路合成单声道（可用第 7/8 路做回声消除）
  → 唤醒/VAD → 识别 → 大模型 → 工具调用
  → 安全仲裁（白名单、状态检查、限速）→ HighController
文字转语音 → publish_external_audio_playback_stream → 扬声器
```

原则：**大模型只做语义理解，不直接输出运动指令**；运动指令必须经过独立的安全仲裁层。

---

## 6. 摄像头

### 6.1 D435i 参数（[RealSense 官方](https://www.realsenseai.com/cn/products/d435i/)）

| 项目 | 数值 |
| --- | --- |
| 深度 | 立体深度，最高 1280×720，最高 90 fps，视场角 87° × 58°，全局快门 |
| 深度精度 / 距离 | 2 m 处 <2%；理想 0.3–3 m；**最大分辨率下最小深度约 28 cm** |
| 彩色 | 1920×1080，30 fps，视场角 69° × 42°，**卷帘快门** |
| IMU | 内置 6 自由度（与机身 IMU 是两个传感器） |
| 接口 | USB 3.1 |

### 6.2 能做什么

| 功能 | 可行性 | 说明 |
| --- | --- | --- |
| 拍照让大模型描述、OCR | 容易 | 取彩色图交给视觉模型 |
| 人/物体检测、测距 | 容易 | Jetson 67 TOPS；深度图配检测框 |
| 前方障碍检测 | 可尝试 | 深度图分扇区；只能作辅助，不是安全保证 |
| 视觉跟随 | 可尝试 | 检测 + 测距，经 HighController 发行走请求 |
| 室内 SLAM | 可研究 | 需额外软件；步行晃动会影响精度；注意相机 IMU 与机身 IMU 的坐标系 |
| 指定地点自主行走 | 需完整系统 | 路径规划、局部避障、安全层 |
| 户外可靠导航 | 无法凭参数确认 | 路面、光照、步态都是问题 |

### 6.3 限制

- 28 cm 内无深度，脚边和低矮障碍看不到；深度视野只有 87° × 58°，侧面和后方是盲区。
- 彩色是卷帘快门，行走晃动时画面有果冻形变。
- 玻璃、镜面、黑色吸光表面、强阳光下深度容易缺失。

### 6.4 与官方服务的关系

- **[官方]** 带算力板的 EDU 机型，相机直接接在算力板上，用 librealsense 访问；使用前先停官方图传服务：`sudo systemctl stop noetix-video-capture.service`（`disable` 取消自启；`start` / `enable` 恢复）。
- **[推断]** 原因是相机被该服务独占打开，可用 `systemctl status`、`lsof /dev/video*`、`rs-enumerate-devices` 验证。停掉后手机 App 的图传应当没有画面，官方语音智能体的视觉功能是否受影响未核实。
- 相机安装位置、朝向、离地高度，以及人在机器人身旁能否被看到，**需要实机确认**。

---

## 7. 手柄与遥控

- **[官方]** 官方手柄为"澳加狮"：带算力板的机型，**接收器插在算力板的 USB 口**，运控板无法插入接收器，所以 DDS 拿不到手柄数据，须在算力板上直接读取（官方 `AoLionDriver`，默认 `/dev/input/js0`，波特率 115200）。
- 官方示例 `test_high.py` 展示了"读手柄 → 生成 `publish_cmd`"的做法，按键编号针对澳加狮。
- 手机 App 与机器人通过**蓝牙**配对（官方警告：连接后不要在系统设置里"遗忘设备"，否则会清除蓝牙记录导致无法再连）；算力板可通过运控板的 WiFi 模块联网，官方给出无线 SSH 方式。App 控制指令具体走哪条链路文档未写。
- 其他 2.4G / Xbox 手柄 **[推断]**：只要算力板上能识别为 `/dev/input/js*`，程序可以自行读取并映射成 `publish_cmd`；Jetson 内核是否带 `xpad` 等驱动需实测。
- 无论哪种手柄，都要做**信号丢失保护**（超过数百毫秒无更新就发 `DEFAULT` 并停止行走）并保留明确的停止键。同时用 App、手柄和程序发指令时谁优先**未验证**，测试时只留一个控制源。

---

## 8. 第三方项目（公开检索，使用前请自行审查代码）

没有一个被确认为"已在 EDU-Pro 上完整实机验证"。使用前请检查：是否默认连接真机、是否会下发动作、是否上传麦克风/摄像头数据。

| 项目 | 玩法 | 备注 |
| --- | --- | --- |
| [lsx-buu/songya-demo-1](https://github.com/lsx-buu/songya-demo-1) | 语音触发 → 拍照 → 视觉大模型 → 语音合成 → 扬声器 | 在 Bumi Max 上部署；现场麦克风/相机/喇叭作者自称尚未验收；动作默认关闭 |
| [tianfeng66/vela](https://github.com/tianfeng66/vela) | 深度相机 + 语音 + 大模型 + 安全仲裁器，有模拟器 | 作者列出现场待确认项；不要当成已验证的导盲设备 |
| [JamesLiang007/ros2_bumi](https://github.com/JamesLiang007/ros2_bumi) | 官方 SDK 封装成 ROS 2 Humble 节点 | 依赖 SDK 2.0.0，不含官方库 |
| [diaskabdualiev/bumi_mujoco](https://github.com/diaskabdualiev/bumi_mujoco) | MuJoCo 仿真，模拟运控板 DDS | **仿真网络必须与真机隔离** |
| [xiaohu97/Bumi-imitation-learning-supermimic](https://github.com/xiaohu97/Bumi-imitation-learning-supermimic) | 动作模仿（Isaac Lab） | 仅仿真验证 |
| [xiaoze-cn/bumi](https://github.com/xiaoze-cn/bumi) | Jetson 跑 SDK，Windows 上位机显示状态 | 只做了状态读取 |
| [szsing/Bumi_Mem](https://github.com/szsing/Bumi_Mem) | 陪伴机器人记忆服务 | 偏 Agent 记忆 |
| [sandraschi/bumi-mcp](https://github.com/sandraschi/bumi-mcp) | MCP 服务 + REST，含模拟桥 | 物理动作未实现 |

---

## 9. 「牵手跟随」可行性

- 室内、平坦、空旷、有人看护的跟随原型：可以研究。
- 牵着出门不能认为二次开发就能安全实现：官方运控"盲走"、禁止复杂路面、禁止运动中外力干扰，而牵手时人的拉力、突然停走和转向本身就是外力。
- **机身 IMU 只适合作辅助**：牵手时机身加速度可能几乎为零，机器人自己迈步也会产生晃动，难以区分"被拉"和"自己走"，被碰撞或将要跌倒时也会有加速度，不能当"跟我走"的指令。更合适的用法是作为异常监测与安全停止的判据。
- 方案 A：视觉跟随（不牵手）；方案 B：专用牵引手柄（需额外力传感器，受力上限须经厂家确认）。建议先做 A 的室内版本。

**必须先问厂家：** HighController 行走时能否保持手臂可牵手的姿态？手臂允许的外力大小和方向？程序停止发送或通信中断时机器人会怎样（不要假设会停）？有没有官方软停止或急停接口？

---

## 10. 同行参考：启元机器人（PrimeBot Q1 / T1）

官方开源：<https://github.com/PrimebotTech/primebot-q1-sdk>、<https://github.com/PrimebotTech/primebot-t1-sdk>。其通信为 ROS 2 Humble + FastDDS，官方明确支持外部电脑直连；开发前需在 App 切换"开发者模式"，且须知写明开启后不再享受维保服务。Bumi 官方文档中我没有找到类似声明，但建议向销售确认。

---

## 11. C# 封装（社区，非官方）

仓库 `noetix_sdk_bumi_csharp`（MIT）：在官方 C++ SDK 外加一层 `extern "C"` 封装，再用 P/Invoke 给 .NET 使用，类名与官方一致（`HighController`、`LowController`、`MediaController`）。官方的 pybind 源码是它的接口清单。验证情况见该仓库的 `docs/verification.md`：**已在 x86_64 Linux 上构建并测试，未在 aarch64 和真机上验证。**

观察到的行为：没有机器人可连时，`HighController.Init()` 会一直阻塞，需放在后台线程并加超时。

---

## 12. 在算力板上的检查清单

目标：在内网里用一台能连机器人的电脑，**只读**地摸清机器情况。**不要访问运控板（192.168.55.102）**，不要在没有吊架时发任何运动指令。

### 12.1 连接

- 网线接机器人背部网口并配同网段静态 IP（**不要用 .101 / .102**），或连机器人的 WiFi。
- `ping 192.168.55.101` 通后，用官方文档给的账号 SSH 登录；无线方式为官方文档中给出的 2222 端口。建议配置密钥登录，不要把密码写进脚本或仓库。

### 12.2 只读检查命令

```bash
# 系统
uname -a; cat /etc/os-release; cat /etc/nv_tegra_release
g++ --version; cmake --version; dotnet --info
free -m; df -h; ip -br addr

# 官方服务与进程
systemctl list-units --type=service --state=running | grep -i noetix
systemctl status noetix-video-capture.service
ps aux | grep -i noetix
ss -tulpn

# 外设
lsusb; ls /dev/video* /dev/input/ 2>/dev/null
arecord -l; aplay -l

# 依赖库与 SDK
ldconfig -p | grep -E 'ssl|crypto|realsense|ddsc'
find ~ -maxdepth 3 -name 'noetix_sdk_bumi*' 2>/dev/null
```

### 12.3 要回答的问题

1. 官方跑了哪些服务？图传服务是否真的占用相机（`rs-enumerate-devices` 在服务运行和停止时的差别）？
2. GCC 版本是否为 11？aarch64 上能否编译封装库？`libssl1.1` 是否已安装？
3. 手柄接收器 / 其他 USB 设备在系统里如何出现？
4. 音频设备是否出现在 ALSA 中，还是只能通过 MediaController 访问？
5. 预装的 SDK 版本是否为 3.1.0？

### 12.4 实测阶段（每一阶段通过再进入下一阶段）

| 阶段 | 内容 | 风险 |
| --- | --- | --- |
| 0 | 编译 SDK 和封装库 | 无 |
| 1 | 只订阅状态（`workmode`、电量、IMU），不发指令；`Init()` 是否会触发动作未验证，**也要上吊架** | 低 |
| 2 | 只读音频：录 10 秒，确认 8 通道、采样率、`format` | 低 |
| 3 | 扬声器播放小段 PCM，先调小音量；必要时关闭官方智能体的回复输出 | 低 |
| 4 | 吊架上发 `DEFAULT`，再发 `SWING` 等无位移动作，再低速 `WALK`；**先测"程序被杀后机器人是否自己停步"** | 高 |

---

## 13. 开放问题（给厂家）

1. EDU-Pro 的相机型号（D435i 还是其变体）、安装位置、朝向、高度，是否提供外参？
2. 带算力板的机器，MediaController 的视频流应如何使用？
3. 音量 0–200 的含义、开机默认值与是否持久化、推荐最大值？
4. HighController 行走时，手臂能否保持固定姿态或被外部指令占用？外力承受范围？
5. 通信中断、程序退出、指令超时的行为和软停止接口？
6. `workmode` 取值表与各动作所需模式？
7. 机身 IMU 的安装位置、坐标系、加速度定义？
8. 是否提供 DDS 的 IDL 和 topic 清单，用于其他语言接入？
9. 4 个麦克风的拾音距离、角度，以及 3A 处理对哪些路径生效？
10. 二次开发是否影响保修？是否有机器人专用吊架或推荐型号、吊挂点？
11. 多个控制源（App、手柄、SDK）同时存在时的优先级？

---

## 14. 来源

**官方**
- 综合使用文档：<https://web.noetixrobotics.com/docs/>
- 产品参数：<https://www.noetixrobotics.com/spec/Bumi>
- SDK：<https://github.com/Noetix-Robotics/noetix_sdk_bumi>（`include/`、`pybind/`、`examples/py/test_high.py`、`examples/py/test_media.py`）
- 强化学习训练：<https://github.com/Noetix-Robotics/noetix_bumi_lab>
- RealSense D435i：<https://www.realsenseai.com/cn/products/d435i/>

## 15. 局限

- 没有接触实体机器人，所有结论基于文档、SDK 源码、第三方项目和少量容器内测试。
- 官方参数页的三项二次开发标注是图标，属推断。
- 第三方仓库只读了 README 和部分文件，没有逐行审计代码。
- 官方文档和 SDK 在更新，请每次动手前核对最新版本。
