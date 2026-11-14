// MIT License - Copyright (c) 2026 Gil Zhang <gil.zhang.dev@outlook.com>
#include "highcontroller.h"
#include "internal.hpp"

using namespace noetix;

namespace {
bumi::Slot<NoetixRobotStatusCallback> g_status;
}

extern "C" {

int32_t noetix_high_init(void) {
    return bumi::guarded([] { return HighController::Instance()->init() ? NOETIX_OK : NOETIX_ERR_INIT_FAILED; });
}

int32_t noetix_high_publish_cmd(double x, double y, double z, uint32_t action, uint16_t index) {
    for (double v : {x, y, z}) {
        if (!std::isfinite(v) || std::fabs(v) > 1.0) {
            return bumi::invalid("x, y, z must be finite and within [-1, 1]");
        }
    }
    if (action > static_cast<uint32_t>(ControlCmd::DEFAULT)) {
        return bumi::invalid("action");
    }
    return bumi::guarded([&] {
        HighController::Instance()->publish_cmd(x, y, z, static_cast<ControlCmd>(action), index);
        return NOETIX_OK;
    });
}

int32_t noetix_high_subscribe_robot_hardware_status(NoetixRobotStatusCallback callback, void* user_data) {
    return bumi::subscribe(g_status, callback, user_data, [] {
        HighController::Instance()->subscribe_robot_hardware_status(
            [](const RobotHardwareStatus& s) { bumi::dispatch_status(g_status, s); });
    });
}

} // extern "C"
