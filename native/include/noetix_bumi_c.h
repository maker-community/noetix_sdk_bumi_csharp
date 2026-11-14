/*
 * MIT License - Copyright (c) 2026 Gil Zhang <gil.zhang.dev@outlook.com>
 *
 * Flat C ABI over the official Noetix Bumi SDK (noetix_sdk_bumi, SDK 3.1.0).
 * Unofficial community binding; not affiliated with Noetix Robotics.
 *
 * Conventions
 *  - Every function returns an int32_t status (NOETIX_OK == 0).
 *  - Official bool results are returned through out parameters.
 *  - Callbacks are invoked on SDK/DDS threads. Pointers handed to a callback
 *    are valid only for the duration of the call; copy what you need.
 *  - Calling a subscribe function again replaces the previous callback;
 *    passing NULL clears it. The official subscription is registered once.
 *  - Only 64-bit targets (aarch64, x86_64) are supported.
 */
#ifndef NOETIX_BUMI_C_H
#define NOETIX_BUMI_C_H

#include <stddef.h>
#include <stdint.h>

#if defined(__GNUC__)
#define NOETIX_BUMI_API __attribute__((visibility("default")))
#else
#define NOETIX_BUMI_API
#endif

#ifdef __cplusplus
extern "C" {
#endif

#define NOETIX_BUMI_C_API_VERSION 1
#define NOETIX_MOTOR_COUNT 21
#define NOETIX_JOY_AXIS_COUNT 2
#define NOETIX_JOY_BUTTON_COUNT 14

/* ---- status codes ------------------------------------------------------ */
#define NOETIX_OK 0
#define NOETIX_ERR_EXCEPTION (-1)        /* SDK threw; see noetix_bumi_last_error */
#define NOETIX_ERR_INVALID_ARGUMENT (-2) /* rejected before reaching the SDK */
#define NOETIX_ERR_INIT_FAILED (-3)      /* official init() returned false */
#define NOETIX_ERR_REJECTED (-4)         /* official call returned false */

/* ---- motion data (layout mirrors noetix::common.h) ---------------------- */
typedef struct NoetixMotorState {
    double pos;
    double vel;
    double tau;
    uint16_t motor_id;
    uint8_t error;
    int32_t temperature;
} NoetixMotorState;

typedef struct NoetixMotorCmd {
    double pos; /* radians */
    double vel;
    double tau;
    double kp;
    double kd;
    uint16_t motor_id;
} NoetixMotorCmd;

typedef struct NoetixImuData {
    double ori[4];
    double ori_cov[9];
    double angular_vel[3];
    double angular_vel_cov[9];
    double linear_acc[3];
    double linear_acc_cov[9];
} NoetixImuData;

typedef struct NoetixJoyData {
    double axes[NOETIX_JOY_AXIS_COUNT];
    int32_t button[NOETIX_JOY_BUTTON_COUNT];
} NoetixJoyData;

typedef struct NoetixBmsData {
    uint16_t battery_temp;
    uint16_t battery_alarm;
    uint16_t battery_soc;
    uint16_t battery_soh;
} NoetixBmsData;

typedef struct NoetixRobotHardwareStatus {
    NoetixImuData imu_data;
    NoetixJoyData remote_data;
    NoetixMotorState motor_data[NOETIX_MOTOR_COUNT];
    NoetixBmsData bms_data;
    int32_t workmode;
} NoetixRobotHardwareStatus;

/* ---- media data --------------------------------------------------------- */
typedef struct NoetixMediaHeader {
    uint64_t message_id;
    uint64_t timestamp_us;
    const char* sn; /* NUL-terminated, never NULL */
} NoetixMediaHeader;

typedef struct NoetixMediaSystemStatus {
    NoetixMediaHeader header;
    int32_t value;  /* media::WorkStatus */
    int32_t reason; /* media::StatusChangeReason */
} NoetixMediaSystemStatus;

typedef struct NoetixMediaSystemError {
    NoetixMediaHeader header;
    int32_t code;
    const char* message; /* NUL-terminated, never NULL */
} NoetixMediaSystemError;

typedef struct NoetixMediaAudioStream {
    NoetixMediaHeader header;
    uint64_t timestamp_us;
    uint32_t channels;
    uint32_t sample_rate;
    uint32_t format;
    uint32_t duration_ms;
    const int16_t* samples;
    uint64_t sample_count;
} NoetixMediaAudioStream;

typedef struct NoetixMediaVideoStream {
    NoetixMediaHeader header;
    uint64_t timestamp_us;
    uint32_t format;
    uint32_t width;
    uint32_t height;
    uint32_t fps;
    const uint8_t* data;
    uint64_t data_size;
} NoetixMediaVideoStream;

#ifdef __cplusplus
static_assert(sizeof(void*) == 8, "64-bit targets only");
static_assert(sizeof(NoetixMotorState) == 32, "layout");
static_assert(sizeof(NoetixMotorCmd) == 48, "layout");
static_assert(sizeof(NoetixImuData) == 296, "layout");
static_assert(sizeof(NoetixJoyData) == 72, "layout");
static_assert(sizeof(NoetixBmsData) == 8, "layout");
static_assert(sizeof(NoetixRobotHardwareStatus) == 1056, "layout");
static_assert(sizeof(NoetixMediaHeader) == 24, "layout");
static_assert(sizeof(NoetixMediaSystemStatus) == 32, "layout");
static_assert(sizeof(NoetixMediaSystemError) == 40, "layout");
static_assert(sizeof(NoetixMediaAudioStream) == 64, "layout");
static_assert(sizeof(NoetixMediaVideoStream) == 64, "layout");
#else
_Static_assert(sizeof(void*) == 8, "64-bit targets only");
_Static_assert(sizeof(NoetixMotorState) == 32, "layout");
_Static_assert(sizeof(NoetixMotorCmd) == 48, "layout");
_Static_assert(sizeof(NoetixImuData) == 296, "layout");
_Static_assert(sizeof(NoetixJoyData) == 72, "layout");
_Static_assert(sizeof(NoetixBmsData) == 8, "layout");
_Static_assert(sizeof(NoetixRobotHardwareStatus) == 1056, "layout");
_Static_assert(sizeof(NoetixMediaHeader) == 24, "layout");
_Static_assert(sizeof(NoetixMediaSystemStatus) == 32, "layout");
_Static_assert(sizeof(NoetixMediaSystemError) == 40, "layout");
_Static_assert(sizeof(NoetixMediaAudioStream) == 64, "layout");
_Static_assert(sizeof(NoetixMediaVideoStream) == 64, "layout");
#endif

/* ---- callbacks ----------------------------------------------------------- */
typedef void (*NoetixRobotStatusCallback)(const NoetixRobotHardwareStatus* status, void* user_data);
typedef void (*NoetixMediaSystemStatusCallback)(const NoetixMediaSystemStatus* status, void* user_data);
typedef void (*NoetixMediaSystemErrorCallback)(const NoetixMediaSystemError* error, void* user_data);
typedef void (*NoetixMediaAudioCallback)(const NoetixMediaAudioStream* stream, void* user_data);
typedef void (*NoetixMediaVideoCallback)(const NoetixMediaVideoStream* stream, void* user_data);

/* ---- library ------------------------------------------------------------- */
NOETIX_BUMI_API int32_t noetix_bumi_c_api_version(void);
/* SDK_VERSION of the official headers this library was built against. */
NOETIX_BUMI_API const char* noetix_bumi_sdk_version(void);
/* Message of the last failure on the calling thread; never NULL. */
NOETIX_BUMI_API const char* noetix_bumi_last_error(void);
/*
 * Sets CYCLONEDDS_URI to "file://<path>" with setenv(3). Must be called before
 * the first init(); Environment.SetEnvironmentVariable does not reach native
 * code on Unix.
 */
NOETIX_BUMI_API int32_t noetix_bumi_set_dds_config(const char* path);

/* ---- HighController ------------------------------------------------------ */
NOETIX_BUMI_API int32_t noetix_high_init(void);
/* x, y, z must be finite and within [-1, 1]; action is a noetix::ControlCmd. */
NOETIX_BUMI_API int32_t noetix_high_publish_cmd(double x, double y, double z, uint32_t action, uint16_t index);
NOETIX_BUMI_API int32_t noetix_high_subscribe_robot_hardware_status(NoetixRobotStatusCallback callback, void* user_data);

/* LowController lives in noetix_bumi_low_c.h / libnoetix_bumi_low_c.so. */

/* ---- MediaController ----------------------------------------------------- */
NOETIX_BUMI_API int32_t noetix_media_init(void);

/* type: media::SystemControlType (0 TO_WAKEUP, 1 TO_SLEEP, 2 TO_RESET) */
NOETIX_BUMI_API int32_t noetix_media_set_system_control(int32_t type, int32_t is_need_reply);
NOETIX_BUMI_API int32_t noetix_media_subscribe_system_status(NoetixMediaSystemStatusCallback callback, void* user_data);
NOETIX_BUMI_API int32_t noetix_media_subscribe_system_error(NoetixMediaSystemErrorCallback callback, void* user_data);

NOETIX_BUMI_API int32_t noetix_media_get_volume(int32_t* value);
NOETIX_BUMI_API int32_t noetix_media_set_volume(int32_t value);
NOETIX_BUMI_API int32_t noetix_media_get_timeout(int32_t* timeout_ms);
NOETIX_BUMI_API int32_t noetix_media_set_timeout(int32_t timeout_ms);

/* Boolean switches: enabled is 0 or 1. */
NOETIX_BUMI_API int32_t noetix_media_get_audio_cue_enable(int32_t* enabled);
NOETIX_BUMI_API int32_t noetix_media_set_audio_cue_enable(int32_t enabled);
NOETIX_BUMI_API int32_t noetix_media_get_internal_capture_audio_data_to_agent_enable(int32_t* enabled);
NOETIX_BUMI_API int32_t noetix_media_set_internal_capture_audio_data_to_agent_enable(int32_t enabled);
NOETIX_BUMI_API int32_t noetix_media_get_external_custom_audio_data_to_agent_enable(int32_t* enabled);
NOETIX_BUMI_API int32_t noetix_media_set_external_custom_audio_data_to_agent_enable(int32_t enabled);
NOETIX_BUMI_API int32_t noetix_media_get_internal_agent_audio_data_to_playback_enable(int32_t* enabled);
NOETIX_BUMI_API int32_t noetix_media_set_internal_agent_audio_data_to_playback_enable(int32_t enabled);
NOETIX_BUMI_API int32_t noetix_media_get_external_custom_audio_data_to_playback_enable(int32_t* enabled);
NOETIX_BUMI_API int32_t noetix_media_set_external_custom_audio_data_to_playback_enable(int32_t enabled);
NOETIX_BUMI_API int32_t noetix_media_get_internal_capture_video_data_to_agent_enable(int32_t* enabled);
NOETIX_BUMI_API int32_t noetix_media_set_internal_capture_video_data_to_agent_enable(int32_t enabled);
NOETIX_BUMI_API int32_t noetix_media_get_external_custom_video_data_to_agent_enable(int32_t* enabled);
NOETIX_BUMI_API int32_t noetix_media_set_external_custom_video_data_to_agent_enable(int32_t enabled);
NOETIX_BUMI_API int32_t noetix_media_get_external_custom_audio_data_to_agent_use_internal_3a(int32_t* enabled);
NOETIX_BUMI_API int32_t noetix_media_set_external_custom_audio_data_to_agent_use_internal_3a(int32_t enabled);

/*
 * String getters copy up to capacity-1 bytes plus a NUL into buffer (buffer may
 * be NULL to only query) and store the full required size, NUL included, in
 * required_size.
 */
NOETIX_BUMI_API int32_t noetix_media_get_wakeup_response_words(char* buffer, uint64_t capacity, uint64_t* required_size);
NOETIX_BUMI_API int32_t noetix_media_set_wakeup_response_words(const char* words);
NOETIX_BUMI_API int32_t noetix_media_get_sleep_response_words(char* buffer, uint64_t capacity, uint64_t* required_size);
NOETIX_BUMI_API int32_t noetix_media_set_sleep_response_words(const char* words);
NOETIX_BUMI_API int32_t noetix_media_get_wakeup_words(char* buffer, uint64_t capacity, uint64_t* required_size);
/* Returns NOETIX_ERR_REJECTED when the official call returns false. */
NOETIX_BUMI_API int32_t noetix_media_add_wakeup_words(const char* words);
NOETIX_BUMI_API int32_t noetix_media_reset_wakeup_words(void);

NOETIX_BUMI_API int32_t noetix_media_subscribe_internal_audio_capture(NoetixMediaAudioCallback callback, void* user_data);
NOETIX_BUMI_API int32_t noetix_media_subscribe_internal_audio_playback(NoetixMediaAudioCallback callback, void* user_data);
NOETIX_BUMI_API int32_t noetix_media_subscribe_internal_video_capture(NoetixMediaVideoCallback callback, void* user_data);
NOETIX_BUMI_API int32_t noetix_media_subscribe_internal_video_desensed(NoetixMediaVideoCallback callback, void* user_data);

NOETIX_BUMI_API int32_t noetix_media_publish_external_video_stream(const NoetixMediaVideoStream* stream);
NOETIX_BUMI_API int32_t noetix_media_publish_external_audio_stream(const NoetixMediaAudioStream* stream);
NOETIX_BUMI_API int32_t noetix_media_publish_external_audio_playback_stream(const NoetixMediaAudioStream* stream);

NOETIX_BUMI_API int32_t noetix_media_pause_video_capture(void);
NOETIX_BUMI_API int32_t noetix_media_resume_video_capture(void);
NOETIX_BUMI_API int32_t noetix_media_pause_audio_capture(void);
NOETIX_BUMI_API int32_t noetix_media_resume_audio_capture(void);
NOETIX_BUMI_API int32_t noetix_media_pause_audio_playback(void);
NOETIX_BUMI_API int32_t noetix_media_resume_audio_playback(void);

#ifdef __cplusplus
} /* extern "C" */
#endif

#endif /* NOETIX_BUMI_C_H */
