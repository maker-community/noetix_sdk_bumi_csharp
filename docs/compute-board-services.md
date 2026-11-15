# Bumi 算力板服务与二次开发 / Compute-board services and custom development

## 中文

### 范围和安全

这是对一台 Bumi EDU-Pro 用户算力板的只读快照，检查日期为 **2026-11-15**；系统为 Ubuntu 22.04.5、
ARM64、`5.15.185-tegra`。系统版本、服务和状态可能随设备、配置或升级变化，不应当视作所有 Bumi 的清单。

- **[实测]** 表示本次在用户算力板观察到；**[官方]** 表示厂商文档明确记载；**[推断]** 表示可能的解释，
  未经实机验证。没有连接或探测运控板，没有运行 SDK 示例、发送运动指令或更改服务。
- 扫描范围主要是算力板的 systemd 系统服务、当前用户服务、定时器、部分进程与监听端口、指定脚本及设备节点。
  普通用户的进程和端口信息可能不完整；Docker 默认 socket 未发现，不等于没有其他容器运行时或容器。
  不能据此排除其他用户服务、未运行程序、应用内后台任务、云端服务或运控板程序。
- 开发者仅操作用户算力板，不访问运控板。不要仅凭名称、端口或 systemd 依赖判断服务可停；停用、禁用或重启任何
  机器人服务之前先告知操作人员并确认影响与恢复方案。开发和动作测试遵循官方安全手册；未经明确许可以及吊架和
  清场确认，不发送运动或使能指令，也不运行 LowController 例程。
- 文件路径、端口和状态仅用于解释这次观察，不是稳定 API。不要公开网络地址、设备标识、日志、APN、
  凭据或视频音频数据。

资料：[官方 Bumi 文档](https://web.noetixrobotics.com/docs/)、
[官方 SDK](https://github.com/Noetix-Robotics/noetix_sdk_bumi)、
[EDU-Pro 二次开发调研](edu-pro-research.md)、[SDK 验证状态](verification.md)。

### 1. 算力板服务：用途及是否可停

`enabled` 表示配置为自启动，`active (running)` 表示进程正在运行；两者都不证明功能健康。
下表为与需求相关的服务，不是系统全部 unit。

| 服务 / 状态 | 用途和证据 | 建议 |
| --- | --- | --- |
| `noetix-video-capture.service` / `active (running)`, `enabled` | **[实测]** 描述为 RealSense DDS Video Capture，运行程序名为 `demo`，启动顺序在 `network-online.target` 后。**[官方]** 带算力板机型直接调用相机前需关闭官方图传程序。 | **有条件可临时停止**，仅在确认需要直接访问相机、告知操作人员并准备恢复后；App 图传及其他消费者可能受影响，影响范围未验证。不要默认禁用自启动。 |
| `network_4g.service` / `active (running)`, `enabled` | **[实测]** 启动 Bash 脚本 `net4g_start.sh`，涉及 SIM 状态、PDP/APN、RNDIS、网络接口、DHCP 和周期性连通性检查，见下节。 | **暂不建议停**；可能影响蜂窝联网及依赖它的功能，实际影响未知。 |
| `NetworkManager.service`、`wpa_supplicant.service`、`ssh.service` / 运行中 | **[实测]** 常见网络与远程访问服务；**[推断]** 更改可能中断连接或通信。 | 不要停；尤其不要在远程操作期间停。 |
| `dbus.service`、`systemd-resolved.service` / 运行中 | **[实测]** 系统消息总线和名称解析服务；`network_4g.service` 声明需要 D-Bus。 | 不要随意停；依赖范围未完全核实。 |
| `nvfancontrol.service`、`nvargus-daemon.service`、`nvs-service.service` 等 / 运行中 | **[实测]** NVIDIA/Jetson 平台服务，描述分别涉及风扇、Argus 和传感器 HAL。 | 不要随意停；可能影响散热或传感器，具体影响未经验证。 |
| `openvpn.service` / `active (exited)`, `enabled` | **[实测]** unit 为已退出状态；这不证明存在可用 VPN 连接，也不证明其属于厂家。 | 用途未确定，不要停。 |
| `ModemManager.service` / 未找到 unit | **[实测]** `network_4g.service` 声明 `Wants` 和 `After`，但当时没有运行的 ModemManager；系统存在 `mmcli`。 | `Wants` 不是运行成功的证明；不要安装、启动或更改来做实验。 |

**[实测]** 对图传和 4G 服务的 systemd 反向依赖查询只列出服务本身；这只说明查询未显示其他 unit 依赖，
不能排除 DDS、网络、设备、脚本或应用层消费者。

### 2. 4G 是不是流量卡？

**[实测：静态分析]** `network_4g.service` 运行的脚本寻找网络接口和调制解调器设备，包含 SIM 状态查询、
PDP/APN 配置及 RNDIS 控制，使用 DHCP，并在循环中检查连接和重试。这是为**蜂窝移动数据联网**编写的逻辑，
而不是仅凭名称猜测的普通 Wi-Fi 服务。脚本本身处于运行状态，不代表蜂窝链路已经连接。

**[未确认]** 本次没有向调制解调器发送查询，也未确定实体模块是否在线、是否插卡、SIM 是否有效、
运营商、资费、当前是否通过蜂窝网络上网，或 App/OTA 是否依赖它。SIM 状态查询是**脚本具备的行为**，
不是本次已查到 SIM 的状态；RNDIS 也不证明具体网络制式。不要为验证而拔卡、发送 AT 指令或重启服务。
如需确认实际套餐与链路，先通过官方 App 或咨询厂家，避免暴露 SIM 标识及账户信息。

**[实测]** 该脚本文件模式显示为所有用户可写；未验证 ACL、挂载状态或实际可写性。不要自行改权限或脚本，
建议向厂家确认这是预期配置及安全更新方案。

### 3. App、局域网控制和 OTA：已知与未知

| 功能 | 证据与限制 | 二次开发建议 |
| --- | --- | --- |
| 手机 App | **[官方]** [NOETIX Run App](https://web.noetixrobotics.com/docs/#三手机app连接设备及操作控制)支持机器人操作，文档有蓝牙连接警告。**[实测]** 算力板上未发现明确命名为 App 控制服务的 systemd unit；不代表该功能不存在。 | 不要推断控制必然经过算力板、Wi-Fi、蓝牙或某个端口。不要在手机系统设置中“遗忘设备”。 |
| 局域网 App 接口 | **[实测]** 本机检查到 TCP 22、53、111 和 UDP 53、111、5353 及多个动态端口；`demo` 当时占用 UDP 7410、7411 和动态端口。部分进程名因权限不可见；图传服务的描述提到 DDS。 | 监听端口和进程名不能证明 App 协议、可访问范围、认证方式或控制 API；端口会变化。不要扫描运控板、向未知端口发包或把 DDS 端口直接当作公开接口。 |
| OTA | **[官方]** 产品参数表标明有 OTA 升级。**[实测]** 检查到标准系统更新定时器，未发现明确命名的 Noetix OTA systemd unit 或定时器。 | 不能据此断言 OTA 不存在、运行位置、更新范围或是否依赖 4G；Ubuntu 更新与机器人 OTA 不是同一回事。向厂家询问升级、回滚及自定义软件保留策略。 |
| 运动服务 | **[官方]** [HighController](https://web.noetixrobotics.com/docs/#五应用开发-highcontroller) 的 SDK 客户端通过 DDS 与运控程序通信，运控程序负责原厂策略。 | SDK 是应用接口，不等于算力板上有一个同名 systemd 服务；不能停止或研究未开放的运控板服务。 |
| 音频/语音 | **[官方]** [MediaController](https://web.noetixrobotics.com/docs/#七音频开发)通过 DDS 与运控板语音模块通信，提供音频流、扬声器及 Agent 配置。 | 算力板上没发现明确对应服务不代表机器人没有语音功能；服务进程、App 路由及云依赖未确认。 |
| 相机与图传 | **[官方]** [相机指南](https://web.noetixrobotics.com/docs/#八相机例程)说明 EDU 相机接算力板，可用 librealsense；直接调用相机前关闭官方图传。 | **[实测]** 存在 `/dev/video0`–`/dev/video3`；普通权限下 `fuser` 未显示占用者，不足以证明无人占用。设备号和图传与 Agent 的关系需实测。 |

**[官方]** 带算力板机型需要持续发布外部视频流给音视频交互系统，才能获取其脱敏视频流；不能据此推断
现有图传服务一定承担了该发布或停止后 Agent 一定失效。**[实测]** 发现的 `rs-fw-update` 是一个文件名，
不能据此当作机器人 OTA 服务或安全地运行固件更新。

### 4. 自定义功能的接入方式

1. **只读状态监控**：在算力板上部署自己的应用，通过 HighController 订阅状态，不调用 `PublishCmd`。
   本仓库提供 [StatusMonitor](../samples/Noetix.Sdk.Bumi.StatusMonitor/Program.cs)；其 `Init()` 无法连通时
   可能长时间阻塞。先按 [构建说明](../README.md#build)准备官方 SDK、原生库及 DDS 配置，并确保进程可找到
   原生依赖。示例用法如下，`timeout` 仅限制该示例进程，不能保证 SDK 内部资源立即释放：

   ```bash
   timeout 30s dotnet run --project samples/Noetix.Sdk.Bumi.StatusMonitor -- /path/to/dds.xml 10
   ```

   将 `/path/to/dds.xml` 换成本机配置；示例和本绑定**尚未在真机或 aarch64 验证**。不要使用 README 中含
   `PublishCmd` 的动作示例作为只读测试。
2. **语音**：通过 `MediaController` 订阅内部麦克风、处理音频或输出到扬声器；启停采集、播放、Agent 配置
   是有副作用的操作，先确认影响。**[官方]** 采集为 8 通道，播放为 2 通道，文档当前列出 16 kHz、
   `format=2`（小端 16 位）；实际以设备及 SDK 返回为准。
3. **视觉**：先评估是否保留官方图传。需要直接访问相机时，按下节的批准与恢复流程操作，再使用 librealsense。
   不要将设备节点编号硬编码为跨设备约定。
4. **局域网自定义应用**：优先让自己开发的服务运行在算力板，并通过官方 SDK 接入；单独设计认证、授权、
   网络隔离、超时和审计。不要假定官方 App 内部协议是公开或稳定的接口。任何未来的动作能力都需要独立的
   安全仲裁，不能让大模型或远程请求直接驱动机器人。

### 5. 可复现的只读检查

以下命令在**已经登录的用户算力板**执行，不需要 `sudo`；输出可能包含主机或网络信息，分享前先脱敏。
不要用 `systemctl cat`、日志或完整进程命令行公开配置、参数和凭据。

```bash
systemctl list-units --type=service --all --no-pager
systemctl list-unit-files --type=service --no-pager
systemctl --user list-units --type=service --all --no-pager
systemctl list-timers --all --no-pager
systemctl show noetix-video-capture.service network_4g.service \
  -p Description -p ActiveState -p SubState -p UnitFileState \
  -p Requires -p Wants -p After -p PartOf
systemctl list-dependencies --reverse --all noetix-video-capture.service
systemctl list-dependencies --reverse --all network_4g.service
ss -lntup
ls -l /dev/video*
```

`ss` 的进程归属可能因权限不完整；上述命令只检查本机，**不要扩展为对运控板的扫描或探测**。
本次没有用停止服务或采集音视频的方式验证依赖。

### 6. 图传服务的受控变更与恢复

**本次未执行以下变更。** 这是基于[官方相机指南](https://web.noetixrobotics.com/docs/#八相机例程)的
人工操作流程，不适用于其他服务：

1. 先告知操作人员并取得明确同意，确认没有人依赖 App 图传或其他相机使用；记录当前 `active` 和 `enabled`
   状态，准备好恢复权限与联系方式。不要进行动作测试。
2. 仅需临时释放相机时，由获授权的操作人员执行 `sudo systemctl stop noetix-video-capture.service`；
   不执行 `disable`。再检查状态和所需功能；如果有异常，停止实验并恢复。
3. 测试结束后**先退出自己的相机程序**，由获授权人员执行
   `sudo systemctl start noetix-video-capture.service`，检查其恢复到原来的状态，并人工确认 App 图传和受影响功能。
   若原本未运行则不要擅自启动；恢复失败时联系厂家。此流程未做实机停启验证。

### 7. 待厂家确认

- App 控制、图传、语音和 OTA 分别位于哪块板或云端？哪些公开接口适合自定义应用？
- 4G 模块与 SIM 的支持情况，是否影响 OTA、远程访问或语音？断网及恢复的预期行为是什么？
- `network_4g` 脚本权限是否为预期配置，升级后是否保留自定义应用？
- 图传服务停止后哪些功能受影响，能否共享相机或订阅官方图传？如何检查恢复成功？
- SDK 通信中断或应用退出后的安全行为、支持的停止机制与多控制源优先级是什么？

---

## English

### Scope and safety

This is a **2026-11-15** read-only snapshot of one Bumi EDU-Pro user compute board: Ubuntu 22.04.5,
ARM64, `5.15.185-tegra`. Versions, units and states vary by device and update. **Observed** means this
board; **official** means the vendor documentation; **inference** requires validation. No motion-board
connection or probe, SDK example, motion command, or service modification was performed.

The inspection covered compute-board systemd units, the current user manager, timers, selected processes,
listeners, scripts and device nodes. Ordinary-user visibility is incomplete. The default Docker socket was
absent, which does not exclude other containers or runtimes. The inspection cannot rule out other users'
services, dormant software, application tasks, cloud services or motion-board processes.

Operate only on the user compute board. Tell the operator and agree on impact and recovery before stopping,
disabling or restarting any robot service. Do not send motion or enable commands without explicit permission,
a gantry and a cleared area; do not use LowController for experiments. Redact network details, identifiers,
logs, APNs, credentials and media before sharing. See the [official documentation](https://web.noetixrobotics.com/docs/),
[official SDK](https://github.com/Noetix-Robotics/noetix_sdk_bumi) and [verification status](verification.md).

### 1. Services and stop guidance

`enabled` means configured for autostart, and `active (running)` means a process is running; neither proves
the feature is healthy. This is a relevant subset, not a complete list of OS units.

| Service / observed state | Evidence and usage | Recommendation |
| --- | --- | --- |
| `noetix-video-capture.service` / running, enabled | Observed description: RealSense DDS Video Capture; process `demo`; ordered after `network-online.target`. The official camera guide says to stop the vendor capture program before direct camera access. | **Conditionally stoppable temporarily**, after operator agreement and a recovery plan. App video and other consumers may be affected; the scope is unverified. Do not disable autostart by default. |
| `network_4g.service` / running, enabled | Runs `net4g_start.sh`, with SIM, APN/PDP, RNDIS, interface, DHCP and connectivity-check logic. | Do not stop yet: possible cellular and dependent-feature impact is unknown. |
| `NetworkManager.service`, `wpa_supplicant.service`, `ssh.service` / running | Network and remote-access services. | Do not stop, especially during remote access. |
| `dbus.service`, `systemd-resolved.service` / running | System bus and name resolution; the 4G unit requires D-Bus. | Do not stop casually; full dependencies are unknown. |
| `nvfancontrol.service`, `nvargus-daemon.service`, `nvs-service.service` and other NVIDIA services / running | Jetson platform services described as fan control, Argus and sensor HAL. | Do not stop casually; cooling and sensors may be affected. |
| `openvpn.service` / `active (exited)`, enabled | Its unit state does not establish a working VPN or vendor ownership. | Purpose unknown; do not stop. |
| `ModemManager.service` / unit not found | The 4G unit declares `Wants` and `After`; no running ModemManager was found, although `mmcli` exists. | A `Wants` relationship does not prove a running modem service. Do not install or start it to experiment. |

Reverse systemd dependency queries for the capture and 4G units listed only the units themselves. This does
not exclude DDS, device, network, script or application-level consumers.

### 2. Does 4G mean a cellular SIM?

**Observed by static inspection:** the 4G script looks for an interface and modem, checks SIM status,
configures PDP/APN and RNDIS, uses DHCP and periodically checks connectivity and retries. This is cellular
data connection logic rather than merely a Wi-Fi service. A running script does not prove the cellular
connection works.

**Unknown:** whether a modem is currently available, a SIM is installed or valid, the carrier or plan,
whether data is currently used, or whether App and OTA depend on it. The inspection did not send modem
queries: SIM checks describe script behavior, not an observed SIM state. RNDIS alone does not establish the
network generation. Do not remove a SIM, issue AT commands or restart the service just to test. Consult
the official App or vendor without disclosing SIM identifiers or account information.

The script's reported mode is world-writable; ACLs, mount status and effective writability were not tested.
Do not modify it or its permissions yourself; ask the vendor whether this is expected and how to update it safely.

### 3. App, LAN and OTA

| Feature | Evidence and limits | Development guidance |
| --- | --- | --- |
| Mobile App | The [official App guide](https://web.noetixrobotics.com/docs/#三手机app连接设备及操作控制) describes robot operation and warns about Bluetooth pairing. No explicitly named App-control systemd unit was found on the compute board. | Do not assume its control path uses this board, Wi-Fi, Bluetooth or a particular port. Do not choose “forget device” in the phone settings. |
| LAN control | Observed TCP listeners: 22, 53, 111; UDP: 53, 111, 5353 and dynamic ports. `demo` owned UDP 7410, 7411 and a dynamic port; some process identities were unavailable. The vendor describes the capture service as DDS. | Ports alone do not establish an App protocol, reachability, authentication or control API. Ports can change. Do not scan the motion board, send packets to unknown services or treat DDS ports as a public API. |
| OTA | The official product table says OTA is supported. Standard OS update timers were present, but no explicitly named Noetix OTA unit or timer was found. | This does not establish whether OTA runs elsewhere, its scope or its 4G dependency. Ubuntu updates are not necessarily robot OTA. Ask the vendor about upgrades, rollback and persistence of custom software. |
| Motion | The official [HighController guide](https://web.noetixrobotics.com/docs/#五应用开发-highcontroller) describes an SDK client communicating over DDS with the factory motion program. | An SDK interface is not necessarily a same-named compute-board service. Do not access or modify motion-board services. |
| Audio and voice | The official [MediaController guide](https://web.noetixrobotics.com/docs/#七音频开发) describes DDS communication with the motion-board voice module and audio/Agent interfaces. | No matching compute-board unit does not mean voice is absent; the processes, App route and cloud dependencies remain unknown. |
| Camera and video | The [official camera guide](https://web.noetixrobotics.com/docs/#八相机例程) places the EDU camera on the compute board and directs users to stop capture before direct access. | `/dev/video0`–`/dev/video3` were present. An unprivileged `fuser` check returned no owners, which does not prove the devices are free. Device numbers and Agent dependencies need verification. |

The vendor says compute-board models must continuously publish external video to the audiovisual system
to receive desensitized video. This does not establish that the existing capture service performs that
publication or that stopping it necessarily breaks the Agent. An `rs-fw-update` filename was observed;
that alone does not identify a robot OTA service or make running a firmware update safe.

### 4. Integration paths

1. **Read-only status:** deploy an app on the compute board and subscribe with HighController without
   calling `PublishCmd`. See [StatusMonitor](../samples/Noetix.Sdk.Bumi.StatusMonitor/Program.cs).
   Prepare the official SDK, native libraries and DDS configuration using the [build instructions](../README.md#build).
   `Init()` can block when disconnected. The command in the Chinese section uses `timeout` to bound the
   example process; replace the placeholder configuration path. This binding and sample have **not been
   verified on a physical robot or aarch64**. Do not run the motion example in the README as a read-only test.
2. **Voice:** use MediaController for microphone subscriptions, audio processing and speaker output.
   Capture/playback controls and Agent configuration have side effects and require impact assessment.
   The official documentation currently lists 8-channel capture, 2-channel playback, 16 kHz and
   `format=2` (little-endian 16-bit); check the actual SDK/device data.
3. **Vision:** decide whether to retain vendor video first. For direct camera access, follow the approved
   capture-service procedure below, then use librealsense. Device-node numbers are not portable identifiers.
4. **Custom LAN app:** host your own service on the compute board and use the official SDK; design separate
   authentication, authorization, network isolation, timeouts and auditing. Do not assume the vendor App
   protocol is public or stable. Future motion features require an independent safety arbiter; do not let an
   LLM or remote request drive the robot directly.

### 5. Read-only inspection and capture recovery

The commands in Chinese section 5 run on the **already logged-in user compute board**, without `sudo`.
Redact output before sharing. `ss` process ownership may be incomplete; do not extend the checks to probe
the motion board. Service dependencies have not been verified by stopping services or collecting media.

The following is a manual procedure based on the [official camera guide](https://web.noetixrobotics.com/docs/#八相机例程);
**it was not executed in this investigation**, and it does not apply to other services:

1. Inform the operator and obtain explicit agreement. Confirm nobody depends on App video or other camera
   consumers; record the original active/enabled states and prepare access and contacts for recovery.
2. If temporary direct camera access is required, an authorized operator may run
   `sudo systemctl stop noetix-video-capture.service`. Do not run `disable`. Check the status and affected
   features; stop the experiment and recover if anything is wrong.
3. Exit the custom camera application first. If the service was originally running, an authorized operator
   runs `sudo systemctl start noetix-video-capture.service`, checks the original state and manually verifies
   App video and affected features. Do not start it if it was originally stopped; contact the vendor if
   recovery fails. This stop/start procedure has not been tested on the robot.

### 6. Questions for the vendor

- Where do App control, video, voice and OTA run, and which interfaces are supported for custom applications?
- Is a cellular modem/SIM supported, and does it affect OTA, remote access or voice? What happens on network loss?
- Is the 4G script's reported permission intentional, and do upgrades retain custom software?
- What consumes capture output; is camera sharing supported, and how should recovery be verified?
- What are the safe behavior on SDK disconnect, supported stop mechanism and priority among control sources?
