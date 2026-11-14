// MIT License - Copyright (c) 2026 Gil Zhang <gil.zhang.dev@outlook.com>
//
// Built into libnoetix_bumi_low_c.so only: the official HighController and
// LowController define the same global symbols and cannot share a library.
#include "lowcontroller.h"
#include "internal.hpp"
#include "noetix_bumi_low_c.h"

using namespace noetix;

namespace {
bumi::Slot<NoetixRobotStatusCallback> g_status;
}

extern "C" {

int32_t noetix_low_init(void) {
    return bumi::guarded([] { return LowController::Instance()->init() ? NOETIX_OK : NOETIX_ERR_INIT_FAILED; });
}

int32_t noetix_low_set_joint(const NoetixMotorCmd* commands, uint32_t count) {
    if (!commands) {
        return bumi::invalid("commands");
    }
    if (count != NOETIX_MOTOR_COUNT) {
        return bumi::invalid("count must be 21");
    }
    return bumi::guarded([&] {
        std::array<MotorCmd, NOETIX_MOTOR_COUNT> cmds;
        for (int i = 0; i < NOETIX_MOTOR_COUNT; ++i) {
            cmds[i].pos = commands[i].pos;
            cmds[i].vel = commands[i].vel;
            cmds[i].tau = commands[i].tau;
            cmds[i].kp = commands[i].kp;
            cmds[i].kd = commands[i].kd;
            cmds[i].motor_id = commands[i].motor_id;
        }
        LowController::Instance()->set_joint(cmds);
        return NOETIX_OK;
    });
}

int32_t noetix_low_subscribe_robot_hardware_status(NoetixRobotStatusCallback callback, void* user_data) {
    return bumi::subscribe(g_status, callback, user_data, [] {
        LowController::Instance()->subscribe_robot_hardware_status(
            [](const RobotHardwareStatus& s) { bumi::dispatch_status(g_status, s); });
    });
}

} // extern "C"
