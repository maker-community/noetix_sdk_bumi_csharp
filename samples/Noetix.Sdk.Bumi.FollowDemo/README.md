# Offline follow simulation / 离线跟随仿真

This experiment does **not** connect to the SDK or a robot. A C# process receives simulated world
positions through standard input and returns commands through standard output. The Python harness
applies them to the MuJoCo walking policy. The orange target is a non-colliding marker; distance
is measured horizontally between the robot base and marker center, not between their surfaces.

本实验**不会连接 SDK 或真实机器人**。C# 程序通过标准输入接收仿真世界坐标、通过标准输出返回指令；
Python 程序将指令用于 MuJoCo 行走策略。橙色目标是无碰撞的标记，距离是机器人基座与标记中心的水平距离，
不是两者表面的距离。

The target walks, approaches, then is synthetically hidden for two seconds. The observation uses
the actual MuJoCo robot pose and the same target position displayed by the viewer, not integrated
commands. The test checks measured separation, that the approach and target-loss scenarios occur,
and zero commands during loss or within 2 m. This is **not camera perception, obstacle avoidance,
physical collision detection, a safety guarantee, or a real-robot controller**. Thresholds are
experiment parameters, not manufacturer-approved safety distances. Target loss causes an immediate
zero command; no automatic search or reverse is attempted.

目标先行走、再靠近，然后被模拟遮挡两秒。观测使用 MuJoCo 的实际机器人位姿和画面中同一目标的位置，
不是根据指令推算。实验检查实际中心间距、靠近和丢目标场景以及丢目标或距离不超过 2 m 时指令归零。
这**不是摄像头感知、避障、真实碰撞检测、安全保证或真机控制程序**；阈值只是实验参数，并非厂商
认可的安全距离。丢目标时立即发送零指令，不自动搜索或后退。

## Run / 运行

On Linux/WSL, install the separately obtained
[community simulator](https://github.com/diaskabdualiev/bumi_mujoco) and its Python dependencies
in a virtual environment. Build the C# sample with .NET 10. Run from this repository root:

在 Linux/WSL 上，单独准备[社区仿真器](https://github.com/diaskabdualiev/bumi_mujoco)及其 Python
虚拟环境，并用 .NET 10 构建 C# 示例。从本仓库根目录执行：

```bash
dotnet build samples/Noetix.Sdk.Bumi.FollowDemo
/path/to/venv/bin/python samples/Noetix.Sdk.Bumi.FollowDemo/simulate_follow.py \
  --simulator-root /path/to/bumi_mujoco \
  --controller "$PWD/samples/Noetix.Sdk.Bumi.FollowDemo/bin/Debug/net10.0/Noetix.Sdk.Bumi.FollowDemo"
```

Add `--view` for a live MuJoCo window. WSLg may crash during viewer shutdown. The experiment uses no
DDS participant and sends no network commands; keep the simulation environment isolated from robot
networks anyway. If model, policy, dependencies or assertions fail, the program exits with an error.

添加 `--view` 可打开实时 MuJoCo 窗口；WSLg 的窗口退出时可能崩溃。该实验不创建 DDS 参与者，
也不通过网络发送指令；仍应让仿真环境与机器人网络隔离。模型、策略、依赖或断言失败时会报错退出。
