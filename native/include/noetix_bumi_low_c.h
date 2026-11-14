/*
 * MIT License - Copyright (c) 2026 Gil Zhang <gil.zhang.dev@outlook.com>
 *
 * LowController (motor-level control, EDU models only) of the unofficial C ABI.
 * Shipped as a separate library, libnoetix_bumi_low_c.so: the official
 * HighController and LowController cannot be linked into the same library.
 * Common types, status codes and library-level functions come from
 * noetix_bumi_c.h and are also exported by the low library.
 *
 * Motor-level commands can damage the robot or hurt people. Use a gantry.
 */
#ifndef NOETIX_BUMI_LOW_C_H
#define NOETIX_BUMI_LOW_C_H

#include "noetix_bumi_c.h"

#ifdef __cplusplus
extern "C" {
#endif

NOETIX_BUMI_API int32_t noetix_low_init(void);
/* count must equal NOETIX_MOTOR_COUNT. */
NOETIX_BUMI_API int32_t noetix_low_set_joint(const NoetixMotorCmd* commands, uint32_t count);
NOETIX_BUMI_API int32_t noetix_low_subscribe_robot_hardware_status(NoetixRobotStatusCallback callback, void* user_data);

#ifdef __cplusplus
} /* extern "C" */
#endif

#endif /* NOETIX_BUMI_LOW_C_H */
