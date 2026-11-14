// MIT License - Copyright (c) 2026 Gil Zhang <gil.zhang.dev@outlook.com>
#include <algorithm>

#include "MediaController.h"
#include "internal.hpp"

using namespace noetix;

namespace {

bumi::Slot<NoetixMediaSystemStatusCallback> g_system_status;
bumi::Slot<NoetixMediaSystemErrorCallback> g_system_error;
bumi::Slot<NoetixMediaAudioCallback> g_audio_capture;
bumi::Slot<NoetixMediaAudioCallback> g_audio_playback;
bumi::Slot<NoetixMediaVideoCallback> g_video_capture;
bumi::Slot<NoetixMediaVideoCallback> g_video_desensed;

MediaController& controller() { return *MediaController::Instance(); }

NoetixMediaHeader to_c(const media::Header& h) {
    NoetixMediaHeader out;
    out.message_id = h.message_id;
    out.timestamp_us = h.timestamp_us;
    out.sn = h.sn.c_str();
    return out;
}

media::Header header_from_c(const NoetixMediaHeader& h) {
    media::Header out;
    out.message_id = h.message_id;
    out.timestamp_us = h.timestamp_us;
    out.sn = h.sn ? h.sn : "";
    return out;
}

media::AudioStream from_c(const NoetixMediaAudioStream& s) {
    media::AudioStream out;
    out.header = header_from_c(s.header);
    out.timestamp_us = s.timestamp_us;
    out.channels = s.channels;
    out.sample_rate = s.sample_rate;
    out.format = s.format;
    out.duration_ms = s.duration_ms;
    if (s.sample_count > 0) {
        out.audio_data.assign(s.samples, s.samples + s.sample_count);
    }
    return out;
}

media::VideoStream from_c(const NoetixMediaVideoStream& s) {
    media::VideoStream out;
    out.header = header_from_c(s.header);
    out.timestamp_us = s.timestamp_us;
    out.format = s.format;
    out.width = s.width;
    out.height = s.height;
    out.fps = s.fps;
    if (s.data_size > 0) {
        out.video_data.assign(s.data, s.data + s.data_size);
    }
    return out;
}

void dispatch_audio(bumi::Slot<NoetixMediaAudioCallback>& slot, const media::AudioStream& s) {
    NoetixMediaAudioStream c;
    c.header = to_c(s.header);
    c.timestamp_us = s.timestamp_us;
    c.channels = s.channels;
    c.sample_rate = s.sample_rate;
    c.format = s.format;
    c.duration_ms = s.duration_ms;
    c.samples = s.audio_data.data();
    c.sample_count = s.audio_data.size();
    bumi::dispatch(slot, &c);
}

void dispatch_video(bumi::Slot<NoetixMediaVideoCallback>& slot, const media::VideoStream& s) {
    NoetixMediaVideoStream c;
    c.header = to_c(s.header);
    c.timestamp_us = s.timestamp_us;
    c.format = s.format;
    c.width = s.width;
    c.height = s.height;
    c.fps = s.fps;
    c.data = s.video_data.data();
    c.data_size = s.video_data.size();
    bumi::dispatch(slot, &c);
}

int32_t copy_string(const std::string& value, char* buffer, uint64_t capacity, uint64_t* required_size) {
    if (required_size) {
        *required_size = value.size() + 1;
    }
    if (buffer && capacity > 0) {
        const size_t n = std::min<size_t>(value.size(), static_cast<size_t>(capacity - 1));
        std::memcpy(buffer, value.data(), n);
        buffer[n] = '\0';
    }
    return NOETIX_OK;
}

} // namespace

extern "C" {

int32_t noetix_media_init(void) {
    return bumi::guarded([] { return controller().init() ? NOETIX_OK : NOETIX_ERR_INIT_FAILED; });
}

int32_t noetix_media_set_system_control(int32_t type, int32_t is_need_reply) {
    if (type < 0 || type > static_cast<int32_t>(media::SystemControlType::TO_RESET)) {
        return bumi::invalid("type");
    }
    return bumi::guarded([&] {
        controller().set_system_control(static_cast<media::SystemControlType>(type), is_need_reply != 0);
        return NOETIX_OK;
    });
}

int32_t noetix_media_subscribe_system_status(NoetixMediaSystemStatusCallback callback, void* user_data) {
    return bumi::subscribe(g_system_status, callback, user_data, [] {
        controller().subscribe_system_status([](const media::SystemStatus& s) {
            NoetixMediaSystemStatus c;
            c.header = to_c(s.header);
            c.value = static_cast<int32_t>(s.value);
            c.reason = static_cast<int32_t>(s.reason);
            bumi::dispatch(g_system_status, &c);
        });
    });
}

int32_t noetix_media_subscribe_system_error(NoetixMediaSystemErrorCallback callback, void* user_data) {
    return bumi::subscribe(g_system_error, callback, user_data, [] {
        controller().subscribe_system_error([](const media::SystemError& e) {
            NoetixMediaSystemError c;
            c.header = to_c(e.header);
            c.code = e.code;
            c.message = e.message.c_str();
            bumi::dispatch(g_system_error, &c);
        });
    });
}

int32_t noetix_media_get_volume(int32_t* value) {
    if (!value) {
        return bumi::invalid("value");
    }
    return bumi::guarded([&] {
        *value = controller().get_volume();
        return NOETIX_OK;
    });
}

int32_t noetix_media_set_volume(int32_t value) {
    return bumi::guarded([&] {
        controller().set_volume(value);
        return NOETIX_OK;
    });
}

int32_t noetix_media_get_timeout(int32_t* timeout_ms) {
    if (!timeout_ms) {
        return bumi::invalid("timeout_ms");
    }
    return bumi::guarded([&] {
        *timeout_ms = controller().get_timeout();
        return NOETIX_OK;
    });
}

int32_t noetix_media_set_timeout(int32_t timeout_ms) {
    return bumi::guarded([&] {
        controller().set_timeout(timeout_ms);
        return NOETIX_OK;
    });
}

#define NOETIX_BOOL_ACCESSORS(name)                                                                    \
    int32_t noetix_media_get_##name(int32_t* enabled) {                                                \
        if (!enabled) {                                                                                \
            return bumi::invalid("enabled");                                                           \
        }                                                                                              \
        return bumi::guarded([&] {                                                                     \
            *enabled = controller().get_##name() ? 1 : 0;                                              \
            return NOETIX_OK;                                                                          \
        });                                                                                            \
    }                                                                                                  \
    int32_t noetix_media_set_##name(int32_t enabled) {                                                 \
        return bumi::guarded([&] {                                                                     \
            controller().set_##name(enabled != 0);                                                     \
            return NOETIX_OK;                                                                          \
        });                                                                                            \
    }

NOETIX_BOOL_ACCESSORS(audio_cue_enable)
NOETIX_BOOL_ACCESSORS(internal_capture_audio_data_to_agent_enable)
NOETIX_BOOL_ACCESSORS(external_custom_audio_data_to_agent_enable)
NOETIX_BOOL_ACCESSORS(internal_agent_audio_data_to_playback_enable)
NOETIX_BOOL_ACCESSORS(external_custom_audio_data_to_playback_enable)
NOETIX_BOOL_ACCESSORS(internal_capture_video_data_to_agent_enable)
NOETIX_BOOL_ACCESSORS(external_custom_video_data_to_agent_enable)
NOETIX_BOOL_ACCESSORS(external_custom_audio_data_to_agent_use_internal_3a)

#undef NOETIX_BOOL_ACCESSORS

int32_t noetix_media_get_wakeup_response_words(char* buffer, uint64_t capacity, uint64_t* required_size) {
    return bumi::guarded(
        [&] { return copy_string(controller().get_wakeup_response_words(), buffer, capacity, required_size); });
}

int32_t noetix_media_set_wakeup_response_words(const char* words) {
    if (!words) {
        return bumi::invalid("words");
    }
    return bumi::guarded([&] {
        controller().set_wakeup_response_words(words);
        return NOETIX_OK;
    });
}

int32_t noetix_media_get_sleep_response_words(char* buffer, uint64_t capacity, uint64_t* required_size) {
    return bumi::guarded(
        [&] { return copy_string(controller().get_sleep_response_words(), buffer, capacity, required_size); });
}

int32_t noetix_media_set_sleep_response_words(const char* words) {
    if (!words) {
        return bumi::invalid("words");
    }
    return bumi::guarded([&] {
        controller().set_sleep_response_words(words);
        return NOETIX_OK;
    });
}

int32_t noetix_media_get_wakeup_words(char* buffer, uint64_t capacity, uint64_t* required_size) {
    return bumi::guarded(
        [&] { return copy_string(controller().get_wakeup_words(), buffer, capacity, required_size); });
}

int32_t noetix_media_add_wakeup_words(const char* words) {
    if (!words) {
        return bumi::invalid("words");
    }
    return bumi::guarded([&] { return controller().add_wakeup_words(words) ? NOETIX_OK : NOETIX_ERR_REJECTED; });
}

int32_t noetix_media_reset_wakeup_words(void) {
    return bumi::guarded([] {
        controller().reset_wakeup_words();
        return NOETIX_OK;
    });
}

int32_t noetix_media_subscribe_internal_audio_capture(NoetixMediaAudioCallback callback, void* user_data) {
    return bumi::subscribe(g_audio_capture, callback, user_data, [] {
        controller().subscribe_internal_audio_capture(
            [](const media::AudioStream s) { dispatch_audio(g_audio_capture, s); });
    });
}

int32_t noetix_media_subscribe_internal_audio_playback(NoetixMediaAudioCallback callback, void* user_data) {
    return bumi::subscribe(g_audio_playback, callback, user_data, [] {
        controller().subscribe_internal_audio_playback(
            [](const media::AudioStream s) { dispatch_audio(g_audio_playback, s); });
    });
}

int32_t noetix_media_subscribe_internal_video_capture(NoetixMediaVideoCallback callback, void* user_data) {
    return bumi::subscribe(g_video_capture, callback, user_data, [] {
        controller().subscribe_internal_video_capture(
            [](const media::VideoStream s) { dispatch_video(g_video_capture, s); });
    });
}

int32_t noetix_media_subscribe_internal_video_desensed(NoetixMediaVideoCallback callback, void* user_data) {
    return bumi::subscribe(g_video_desensed, callback, user_data, [] {
        controller().subscribe_internal_video_desensed(
            [](const media::VideoStream s) { dispatch_video(g_video_desensed, s); });
    });
}

int32_t noetix_media_publish_external_video_stream(const NoetixMediaVideoStream* stream) {
    if (!stream || (stream->data_size > 0 && !stream->data)) {
        return bumi::invalid("stream");
    }
    return bumi::guarded([&] {
        controller().publish_external_video_stream(from_c(*stream));
        return NOETIX_OK;
    });
}

int32_t noetix_media_publish_external_audio_stream(const NoetixMediaAudioStream* stream) {
    if (!stream || (stream->sample_count > 0 && !stream->samples)) {
        return bumi::invalid("stream");
    }
    return bumi::guarded([&] {
        controller().publish_external_audio_stream(from_c(*stream));
        return NOETIX_OK;
    });
}

int32_t noetix_media_publish_external_audio_playback_stream(const NoetixMediaAudioStream* stream) {
    if (!stream || (stream->sample_count > 0 && !stream->samples)) {
        return bumi::invalid("stream");
    }
    return bumi::guarded([&] {
        controller().publish_external_audio_playback_stream(from_c(*stream));
        return NOETIX_OK;
    });
}

#define NOETIX_MEDIA_COMMAND(name)                                                                     \
    int32_t noetix_media_##name(void) {                                                                \
        return bumi::guarded([] {                                                                      \
            controller().name();                                                                       \
            return NOETIX_OK;                                                                          \
        });                                                                                            \
    }

NOETIX_MEDIA_COMMAND(pause_video_capture)
NOETIX_MEDIA_COMMAND(resume_video_capture)
NOETIX_MEDIA_COMMAND(pause_audio_capture)
NOETIX_MEDIA_COMMAND(resume_audio_capture)
NOETIX_MEDIA_COMMAND(pause_audio_playback)
NOETIX_MEDIA_COMMAND(resume_audio_playback)

#undef NOETIX_MEDIA_COMMAND

} // extern "C"
