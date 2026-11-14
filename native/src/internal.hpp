// MIT License - Copyright (c) 2026 Gil Zhang <gil.zhang.dev@outlook.com>
//
// Internal helpers shared by the native translation units (not a public header).
#pragma once

#include <array>
#include <atomic>
#include <chrono>
#include <cmath>
#include <cstring>
#include <functional>
#include <memory>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

#include "common.h" // official SDK
#include "noetix_bumi_c.h"

namespace bumi {

inline thread_local std::string last_error;

inline void set_error(const char* message) { last_error = message ? message : ""; }

inline int32_t invalid(const char* what) {
    last_error = std::string("invalid argument: ") + what;
    return NOETIX_ERR_INVALID_ARGUMENT;
}

// Runs f (returning a status code) and converts exceptions to status codes.
template <typename F> int32_t guarded(F&& f) {
    try {
        return f();
    } catch (const std::exception& e) {
        set_error(e.what());
    } catch (...) {
        set_error("unknown exception");
    }
    return NOETIX_ERR_EXCEPTION;
}

// Holds the callback currently installed by the caller. The official
// subscription is registered once; later subscribe calls only swap the pointer.
template <typename Cb> struct Slot {
    std::mutex mutex;
    Cb callback = nullptr;
    void* user = nullptr;
    std::mutex register_mutex;
    bool registered = false;
};

template <typename Cb, typename Register>
int32_t subscribe(Slot<Cb>& slot, Cb callback, void* user, Register&& register_official) {
    return guarded([&]() -> int32_t {
        {
            std::lock_guard<std::mutex> lock(slot.mutex);
            slot.callback = callback;
            slot.user = user;
        }
        std::lock_guard<std::mutex> lock(slot.register_mutex);
        if (!slot.registered) {
            register_official();
            slot.registered = true;
        }
        return NOETIX_OK;
    });
}

template <typename Cb, typename... Args> void dispatch(Slot<Cb>& slot, const Args*... args) {
    Cb callback;
    void* user;
    {
        std::lock_guard<std::mutex> lock(slot.mutex);
        callback = slot.callback;
        user = slot.user;
    }
    if (callback) {
        callback(args..., user);
    }
}

inline void to_c(const noetix::RobotHardwareStatus& in, NoetixRobotHardwareStatus& out) {
    std::memset(&out, 0, sizeof(out));
    std::memcpy(out.imu_data.ori, in.imu_data.ori, sizeof(out.imu_data.ori));
    std::memcpy(out.imu_data.ori_cov, in.imu_data.ori_cov, sizeof(out.imu_data.ori_cov));
    std::memcpy(out.imu_data.angular_vel, in.imu_data.angular_vel, sizeof(out.imu_data.angular_vel));
    std::memcpy(out.imu_data.angular_vel_cov, in.imu_data.angular_vel_cov,
                sizeof(out.imu_data.angular_vel_cov));
    std::memcpy(out.imu_data.linear_acc, in.imu_data.linear_acc, sizeof(out.imu_data.linear_acc));
    std::memcpy(out.imu_data.linear_acc_cov, in.imu_data.linear_acc_cov,
                sizeof(out.imu_data.linear_acc_cov));

    std::memcpy(out.remote_data.axes, in.remote_data.axes, sizeof(out.remote_data.axes));
    for (int i = 0; i < NOETIX_JOY_BUTTON_COUNT; ++i) {
        out.remote_data.button[i] = in.remote_data.button[i];
    }

    for (int i = 0; i < NOETIX_MOTOR_COUNT; ++i) {
        const noetix::MotorState& m = in.motor_data[i];
        NoetixMotorState& o = out.motor_data[i];
        o.pos = m.pos;
        o.vel = m.vel;
        o.tau = m.tau;
        o.motor_id = m.motor_id;
        o.error = m.error;
        o.temperature = m.temperature;
    }

    out.bms_data.battery_temp = in.bms_data.battery_temp_;
    out.bms_data.battery_alarm = in.bms_data.battery_alarm_;
    out.bms_data.battery_soc = in.bms_data.battery_soc_;
    out.bms_data.battery_soh = in.bms_data.battery_soh_;
    out.workmode = in.workmode;
}

inline void dispatch_status(Slot<NoetixRobotStatusCallback>& slot, const noetix::RobotHardwareStatus& status) {
    NoetixRobotHardwareStatus converted;
    to_c(status, converted);
    dispatch(slot, &converted);
}

} // namespace bumi
