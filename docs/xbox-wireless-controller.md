# Bumi 算力板 Xbox 无线手柄 / Xbox wireless controller on the Bumi compute board

[中文](#中文) | [English](#english)

## 中文

本文介绍 Microsoft Xbox 无线接收器的一种**社区方案**，并非 Noetix 官方支持的手柄集成方案，也不是微软官方 Linux 驱动。
该驱动只提供 Linux 输入设备，不代表手柄已接入 Noetix SDK，也不能证明机器人会安全地响应手柄。

### 安全须知

- 加载驱动、配对、重启或按手柄按钮前，先将机器人固定在吊架上并清空周围区域。遵循厂商安全流程；
  机器人悬空时不要启用步态。
- 已配对的手柄可能自动重连。即使测试程序只读取事件，其他现有程序也可能接收输入并使能或移动机器人；
  不能假定没有程序在监听。
- 机器人未固定、测试未准备好之前，不要操作摇杆或按钮，并确保急停措施可用。
- 只操作面向用户的算力板，不访问运控板。停止或重启机器人服务前必须告知操作人员。

### 实测环境与结果

以下为 **2026-11-15 在一台 Bumi 算力板上的实测**，不能保证适用于其他设备或软件版本：

| 项目 | 实测结果 |
| --- | --- |
| 平台 | NVIDIA Jetson Orin Nano，aarch64；Ubuntu 22.04.5 LTS；NVIDIA L4T R36.5.0 |
| 运行中的内核 | `5.15.185-tegra` |
| 接收器 | Microsoft XBOX ACC，USB ID `045e:02fe` |
| 手柄 | `Microsoft Xbox Controller`，输入设备 ID `045e:0b12` |
| 驱动 | 社区项目 [dlundqvist/xone](https://github.com/dlundqvist/xone)，标签 `v0.4.12`，提交 `ee5af99ed8eb115697614845ad82729cb3089c3e` |
| 编译 | 系统已有匹配的内核构建目录和 GCC。默认编译因 C99 风格的循环变量声明失败；使用 `KCFLAGS=-std=gnu11` 后编译成功，但有警告。 |
| 连接 | 接收器绑定 `xone-dongle`；配对手柄出现为 `/dev/input/event6`、`/dev/input/js0` 和 `js1`。设备编号可能变化。 |
| 输入 | 只读摇杆测试观察到按钮按下和释放事件；没有发送运动或使能指令。 |
| 重启 | 按下述方式安装并索引模块后，算力板重启时模块自动加载，手柄再次被识别。 |

这仅验证了 Linux 设备枚举和观察到的按钮事件，**未验证** Noetix SDK 集成、机器人运动、所有按钮或轴、
力反馈，或其他内核、手柄和开发板的兼容性。

### 版本与安装方式

原项目 [medusalix/xone](https://github.com/medusalix/xone) 已进入维护模式，并指向社区分支。
社区分支的 [v0.5.0 发布说明](https://github.com/dlundqvist/xone/releases/tag/v0.5.0)指出，
0.5.x 不再兼容低于 6.5 的内核，旧内核建议使用 0.4.x。
[v0.4.12 文档](https://github.com/dlundqvist/xone/tree/v0.4.12)列出 Linux 5.13+ 并支持无线接收器；
这些版本要求不代表已验证所有 Tegra 内核。

以下方案只为**当前内核**安装本次接收器和手柄需要的三个模块，不安装 DKMS、不配置开机服务、
不将 `xpad` 或 `mt76x2u` 加入黑名单。上游安装脚本会屏蔽这两个驱动，可能影响其他设备。
此最小方案不会在内核更新后自动重新编译：更新后必须重新评估兼容性，使用匹配的内核头文件编译并安装。
它也不是 xone 有线、音频或其他配件驱动的完整安装。

### 为实测内核编译和安装

以下操作会以管理员权限修改算力板。执行前请审阅社区源码及微软固件使用条款。
保持手柄关闭、机器人固定；手柄随后连接时，现有机器人程序可能感知到它。不要在运控板上执行。

1. 检查内核及其对应的构建目录：

   ```sh
   uname -r
   ls -ld /lib/modules/$(uname -r)/build
   ```

   以下命令仅在 `5.15.185-tegra` 上实测；如果内核不同，请停止并重新评估。
   构建目录存在本身不保证其配置和符号与运行中的内核一致。

2. 确认已安装 `git`、`make`、GCC、`curl`、`cabextract` 和匹配的内核头文件。
   实测板子原本已有编译器、构建目录、git 和 curl，`cabextract` 是单独安装的。
   安装软件包需要管理员权限，且可能影响系统。

3. 将固定版本克隆到新目录，仅编译，不加载模块：

   ```sh
   test ! -e "$HOME/xone-v0.4.12-build" &&
     git clone --depth 1 --branch v0.4.12 https://github.com/dlundqvist/xone.git "$HOME/xone-v0.4.12-build" &&
     cd "$HOME/xone-v0.4.12-build" &&
     test "$(git rev-parse HEAD)" = "ee5af99ed8eb115697614845ad82729cb3089c3e" &&
     nice -n 15 make -j2 -C /lib/modules/$(uname -r)/build M="$PWD" KCFLAGS=-std=gnu11 modules
   ```

   任何命令失败都应停止；不要未经检查就复用已有目录。不要运行 `make test`，上游的该目标会加载模块。
   实测有编译器版本及混合声明警告；出现错误或新警告时应检查，不应直接认为构建有效。

4. 机器人固定后，使用该版本的脚本安装专有接收器固件。脚本会显示微软使用条款并要求操作人员确认，
   下载和校验两个固件文件，其中 `/lib/firmware/xow_dongle.bin` 用于本次接收器：

   ```sh
   sudo ./install/firmware.sh
   ```

   下载、解压或哈希校验失败时应停止，不要重新分发固件。

5. 安装三个已编译模块并重建模块索引。以下命令检查实测内核，并拒绝覆盖指定的已有目录：

   ```sh
   test "$(uname -r)" = "5.15.185-tegra" &&
     test ! -e /lib/modules/5.15.185-tegra/extra/noetix-xone-0.4.12 &&
     sudo install -d -m 755 /lib/modules/5.15.185-tegra/extra/noetix-xone-0.4.12 &&
     sudo install -m 644 xone_gip.ko xone_gip_gamepad.ko xone_dongle.ko \
       /lib/modules/5.15.185-tegra/extra/noetix-xone-0.4.12/ &&
     sudo depmod -a 5.15.185-tegra
   ```

   失败时应停止，检查错误和可能部分创建的目标目录后再决定如何重试。安装文件不代表自动加载一定成功。

6. 接收器已插入时，检查模块匹配，不加载模块：

   ```sh
   modinfo -n xone_dongle
   modprobe -R 'usb:v045Ep02FEd0100dc00dsc00dp00icFFiscFFipFFin00'
   modprobe -R 'gip:Windows.Xbox.Input.Gamepad'
   modprobe --show-depends xone_dongle
   ```

   上面的 USB modalias 来自实测板子；若不同，请从 sysfs 获取实际接口的 `modalias`。
   预期匹配模块为 `xone_dongle` 和 `xone_gip_gamepad`。这些检查不能证明模块一定加载成功或固件已就绪。

7. 操作人员可在遵循厂商安全流程的前提下重启**算力板**。重启会中断连接并重启板上服务，
   也可能使已配对手柄自动重连；安全测试条件具备之前保持手柄关闭。

### 配对与检查

机器人固定后，使用接收器和 Xbox 手柄的实体配对键进行配对。之前通过蓝牙或 USB 连接过手柄，
不代表它已与这个接收器配对；参见 [xone 配对说明](https://github.com/dlundqvist/xone/tree/v0.4.12#wireless-pairing)。
配对期间避免操作其他按钮和摇杆。

以下只读检查不会发送手柄输入：

```sh
lsusb
grep '^xone_' /proc/modules
cat /proc/bus/input/devices
ls -l /dev/input/by-id/
```

查找 USB `045e:02fe`、`xone_dongle`、`xone_gip`、`xone_gip_gamepad` 和
`Microsoft Xbox Controller` 输入设备。只看到 USB 接收器不能证明手柄已连接。
`js` 和 `event` 编号在运行时分配，应根据设备名称及 `/dev/input/by-id/` 链接确认，
不要假定一定是 `event6` 或 `js0`。

要确认接收器使用的驱动，可在 `/sys/bus/usb/devices/` 下找到接收器接口并查看其 `driver` 符号链接。
实测板子的接口链接到 `/sys/bus/usb/drivers/xone-dongle`。
若接口的 `pairing` 属性可读，值 `0` 只表示当前不处于配对模式，并非已保存配对的列表。

#### 可选：按钮测试

仅在机器人已固定且操作人员了解其他程序可能收到同一输入时测试按钮。
不要通过测试使能机器人或发送运动指令。如果已安装 `joystick` 软件包且对应设备可读，可执行：

```sh
timeout 30s jstest --event /dev/input/js0
```

将 `js0` 替换为已确认的手柄设备；只按下并释放需要测试的按钮，不要移动摇杆。
命令会显示摇杆事件，包括可能出现的轴事件。30 秒到期时 `timeout` 通常返回状态码 124。
若权限不足或缺少 `jstest`，应明确处理；评估影响之前不要更改设备权限或安装软件包。

### 故障排查与限制

- `lsusb` 中没有接收器：检查物理连接。USB 可识别并不代表驱动或固件正常工作。
- 有接收器但未绑定驱动：检查已安装模块的别名、匹配的内核版本、依赖和内核日志。
  加载或绑定失败需要调查；不要反复安装或将其他驱动加入黑名单。
- 驱动已绑定但没有手柄输入：在安全条件下检查手柄电源及其与接收器的配对。
  没有当前连接不能证明没有保存配对。
- `jstest` 无法打开设备：检查实际设备节点和权限。设备名及权限因系统而异；不要读取无关输入设备。
- 更新内核可能使手动安装的模块失效。保留匹配的头文件并针对新内核重新编译，不要假定旧 `.ko` 仍然兼容。

此方案会在板子上保留固件和模块。后续删除或回滚时需考虑其他使用它们的应用和设备；
更改正在运行的服务或驱动前应告知操作人员。

---

## English

### Xbox wireless controller on the Bumi compute board

This is a **community** setup for a Microsoft Xbox Wireless Adapter, not a Noetix-supported controller integration or
Microsoft's official Linux driver. The driver exposes Linux input devices; it does not by itself integrate the controller
with the Noetix SDK or establish that the robot will respond safely to it.

### Safety first

- Before loading a driver, pairing, rebooting, or pressing controller buttons, secure the robot on a gantry and clear
  the surrounding area. Follow the manufacturer's safety procedures; do not activate a gait while suspended.
- A previously paired controller may reconnect automatically. Existing programs can receive its input and may enable
  or move the robot, even when the test itself only reads events. Do not assume that no program is listening.
- Do not move the sticks or press buttons until the robot is secured and the test is ready. Keep an appropriate
  emergency stop available.
- Work only on the user compute board, not the motion-control board. Do not stop or restart robot services without
  informing the operator.

### Observed environment and results

**Observed on one Bumi compute board on 2026-11-15**, not a guarantee for other boards or software versions:

| Item | Observation |
| --- | --- |
| Platform | NVIDIA Jetson Orin Nano, aarch64; Ubuntu 22.04.5 LTS; NVIDIA L4T R36.5.0 |
| Running kernel | `5.15.185-tegra` |
| Receiver | Microsoft XBOX ACC, USB ID `045e:02fe` |
| Controller | `Microsoft Xbox Controller`, input product ID `045e:0b12` |
| Driver | Community [dlundqvist/xone](https://github.com/dlundqvist/xone), tag `v0.4.12`, commit `ee5af99ed8eb115697614845ad82729cb3089c3e` |
| Build | The matching kernel build tree and GCC were present. Building with `KCFLAGS=-std=gnu11` succeeded with warnings; the default build failed on C99-style loop declarations. |
| Connection | The receiver bound to `xone-dongle`; the paired controller appeared as `/dev/input/event6` and `/dev/input/js0` (also `js1`). Device numbers can change. |
| Input | Read-only joystick tests observed button press and release events. No motion or enable commands were sent. |
| Restart | After the modules were installed and indexed as described below, they loaded automatically after a board restart and the controller was detected again. |

This verifies Linux device enumeration and observed button events only. It does **not** verify Noetix SDK integration,
robot motion, every button or axis, force feedback, or compatibility with another kernel, controller, or board.

### Version and installation choice

The original [medusalix/xone](https://github.com/medusalix/xone) project is in maintenance mode and points to the
community fork. The fork's [v0.5.0 release notes](https://github.com/dlundqvist/xone/releases/tag/v0.5.0) state that
0.5.x breaks compatibility with kernels older than 6.5 and recommend 0.4.x for older kernels. The
[v0.4.12 README](https://github.com/dlundqvist/xone/tree/v0.4.12) lists Linux 5.13+ and supports the wireless dongle.
Those version requirements do not prove compatibility with every Tegra kernel.

The procedure below installs only the three modules needed for the observed receiver and gamepad on the **current
kernel**. It does not install DKMS, configure a boot service, or blacklist `xpad` or `mt76x2u`. The upstream installer
does blacklist those drivers, which can affect other devices. The minimal installation does not automatically rebuild
modules after a kernel update; re-evaluate compatibility, rebuild against matching headers and reinstall for the new
kernel. It is not a full installation of xone's wired, audio, or other accessory drivers.

### Build and install for the observed kernel

These are administrator-level changes to the compute board. Review the community source and Microsoft's firmware terms
before proceeding. Keep the controller off and the robot secured; connecting it later may be visible to existing robot
software. Do not run these commands against the motion-control board.

1. Check that the kernel and its matching build tree are available:

   ```sh
   uname -r
   ls -ld /lib/modules/$(uname -r)/build
   ```

   The following commands were tested with `5.15.185-tegra`; stop and reassess on a different kernel. A build tree
   existing alone does not guarantee that its configuration and symbols match the running kernel.

2. Ensure `git`, `make`, GCC, `curl`, `cabextract`, and matching kernel headers are installed. On the observed board,
   the compiler, build tree, git, and curl were already present; `cabextract` was installed separately. Package
   installation needs administrator permission and may affect the system.

3. Clone the pinned release into a new directory and build without loading any modules:

   ```sh
   test ! -e "$HOME/xone-v0.4.12-build" &&
     git clone --depth 1 --branch v0.4.12 https://github.com/dlundqvist/xone.git "$HOME/xone-v0.4.12-build" &&
     cd "$HOME/xone-v0.4.12-build" &&
     test "$(git rev-parse HEAD)" = "ee5af99ed8eb115697614845ad82729cb3089c3e" &&
     nice -n 15 make -j2 -C /lib/modules/$(uname -r)/build M="$PWD" KCFLAGS=-std=gnu11 modules
   ```

   Stop if any command fails; do not reuse an existing directory without inspecting it. Do not run `make test`: the
   upstream target loads modules. Compiler version and mixed-declaration warnings were observed; inspect any errors or
   new warnings rather than assuming a build is valid.

4. With the robot secured, install the proprietary receiver firmware using the release's script. It displays Microsoft's
   terms and requires operator confirmation. It downloads and verifies two firmware files, including
   `/lib/firmware/xow_dongle.bin` for this receiver:

   ```sh
   sudo ./install/firmware.sh
   ```

   Stop on download, extraction, or hash verification errors. Do not redistribute the firmware.

5. Install the three built modules and rebuild the module index. This checks the tested kernel and refuses to overwrite
   the named destination directory:

   ```sh
   test "$(uname -r)" = "5.15.185-tegra" &&
     test ! -e /lib/modules/5.15.185-tegra/extra/noetix-xone-0.4.12 &&
     sudo install -d -m 755 /lib/modules/5.15.185-tegra/extra/noetix-xone-0.4.12 &&
     sudo install -m 644 xone_gip.ko xone_gip_gamepad.ko xone_dongle.ko \
       /lib/modules/5.15.185-tegra/extra/noetix-xone-0.4.12/ &&
     sudo depmod -a 5.15.185-tegra
   ```

   If a command fails, stop and inspect the error and any partially created destination before retrying. This step
   installs files but does not itself prove that automatic loading will work.

6. With the receiver plugged in, check module resolution without loading anything:

   ```sh
   modinfo -n xone_dongle
   modprobe -R 'usb:v045Ep02FEd0100dc00dsc00dp00icFFiscFFipFFin00'
   modprobe -R 'gip:Windows.Xbox.Input.Gamepad'
   modprobe --show-depends xone_dongle
   ```

   The USB modalias shown is the one observed on this board; obtain the actual interface `modalias` from sysfs if it
   differs. The expected matching modules are `xone_dongle` and `xone_gip_gamepad`. These checks do not prove the
   modules will load successfully or that firmware is ready.

7. An operator may restart the **compute board** under the manufacturer's safety procedures. Restarting interrupts
   connectivity and restarts its services; it can also allow an already paired controller to reconnect. Keep the
   controller off until it is safe to test.

### Pair and check

With the robot secured, pair the Xbox controller with the receiver using their physical pairing buttons. A previous
Bluetooth or USB connection does not establish a pairing with this receiver; see the
[xone pairing instructions](https://github.com/dlundqvist/xone/tree/v0.4.12#wireless-pairing). Avoid other buttons and
sticks during pairing.

The following checks are read-only and do not send controller input:

```sh
lsusb
grep '^xone_' /proc/modules
cat /proc/bus/input/devices
ls -l /dev/input/by-id/
```

Look for USB `045e:02fe`, `xone_dongle`, `xone_gip`, `xone_gip_gamepad`, and a `Microsoft Xbox Controller` input
device. A USB receiver alone does not prove a controller is connected. The `js` and `event` numbers are assigned at
runtime; use the input device name and `/dev/input/by-id/` links rather than assuming `event6` or `js0`.

To confirm which driver owns the receiver, locate its interface under `/sys/bus/usb/devices/` and inspect its `driver`
symlink. On the observed board the interface linked to `/sys/bus/usb/drivers/xone-dongle`. Pairing mode `0`, if readable
from the interface's `pairing` attribute, only means pairing mode is off; it is not a list of saved pairings.

#### Optional button test

Only test buttons while the robot is secured and the operator understands that other programs may receive the same
input. Do not use the test to enable or command robot motion. If the `joystick` package is installed and the matching
joystick device is readable, an operator can use:

```sh
timeout 30s jstest --event /dev/input/js0
```

Replace `js0` with the identified controller device. Press and release only the buttons being tested; do not move the
sticks. The command displays joystick events, including any axis events. `timeout` normally returns exit status 124
when the 30 seconds expire. Missing permissions or a missing `jstest` must be addressed explicitly; do not change
device permissions or install packages without assessing their impact.

### Troubleshooting and limits

- Receiver missing from `lsusb`: check the physical connection. Recognition as USB alone does not establish that the
  driver or firmware is working.
- Receiver present but no driver binding: check the installed module's alias, matching kernel version, module
  dependencies, and kernel logs. Module loading or binding failures require investigation; do not repeatedly install or
  blacklist other drivers.
- Driver bound but no controller input: check the controller's power and receiver pairing in a safe setup. Do not
  infer that saved pairings are absent merely because no controller is currently connected.
- `jstest` cannot open the device: check the exact node and its permissions. Device names and permissions vary by
  system; never read unrelated input devices.
- A new kernel can invalidate the manually installed modules. Preserve the matching headers and rebuild for the new
  kernel; do not assume the previous `.ko` files remain compatible.

The setup leaves firmware and modules on the board. Any later removal or rollback must account for other applications
and devices using them; inform the operator before changing running services or drivers.
