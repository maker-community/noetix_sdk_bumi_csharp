using System.Runtime.InteropServices;

namespace Noetix.Sdk.Bumi.Native;

// One method per function in native/include/noetix_bumi_c.h (same names).
internal static unsafe partial class NativeMethods
{
    internal const string Library = "noetix_bumi_c";

    // ---- library
    [LibraryImport(Library)] internal static partial int noetix_bumi_c_api_version();
    [LibraryImport(Library)] internal static partial byte* noetix_bumi_sdk_version();
    [LibraryImport(Library)] internal static partial byte* noetix_bumi_last_error();
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)] internal static partial int noetix_bumi_set_dds_config(string path);

    // ---- HighController
    [LibraryImport(Library)] internal static partial int noetix_high_init();
    [LibraryImport(Library)] internal static partial int noetix_high_publish_cmd(double x, double y, double z, uint action, ushort index);
    [LibraryImport(Library)] internal static partial int noetix_high_subscribe_robot_hardware_status(delegate* unmanaged<RobotHardwareStatus*, nint, void> callback, nint userData);

    // ---- MediaController
    [LibraryImport(Library)] internal static partial int noetix_media_init();
    [LibraryImport(Library)] internal static partial int noetix_media_set_system_control(int type, int isNeedReply);
    [LibraryImport(Library)] internal static partial int noetix_media_subscribe_system_status(delegate* unmanaged<NativeMediaSystemStatus*, nint, void> callback, nint userData);
    [LibraryImport(Library)] internal static partial int noetix_media_subscribe_system_error(delegate* unmanaged<NativeMediaSystemError*, nint, void> callback, nint userData);

    [LibraryImport(Library)] internal static partial int noetix_media_get_volume(int* value);
    [LibraryImport(Library)] internal static partial int noetix_media_set_volume(int value);
    [LibraryImport(Library)] internal static partial int noetix_media_get_timeout(int* timeoutMs);
    [LibraryImport(Library)] internal static partial int noetix_media_set_timeout(int timeoutMs);

    [LibraryImport(Library)] internal static partial int noetix_media_get_audio_cue_enable(int* enabled);
    [LibraryImport(Library)] internal static partial int noetix_media_set_audio_cue_enable(int enabled);
    [LibraryImport(Library)] internal static partial int noetix_media_get_internal_capture_audio_data_to_agent_enable(int* enabled);
    [LibraryImport(Library)] internal static partial int noetix_media_set_internal_capture_audio_data_to_agent_enable(int enabled);
    [LibraryImport(Library)] internal static partial int noetix_media_get_external_custom_audio_data_to_agent_enable(int* enabled);
    [LibraryImport(Library)] internal static partial int noetix_media_set_external_custom_audio_data_to_agent_enable(int enabled);
    [LibraryImport(Library)] internal static partial int noetix_media_get_internal_agent_audio_data_to_playback_enable(int* enabled);
    [LibraryImport(Library)] internal static partial int noetix_media_set_internal_agent_audio_data_to_playback_enable(int enabled);
    [LibraryImport(Library)] internal static partial int noetix_media_get_external_custom_audio_data_to_playback_enable(int* enabled);
    [LibraryImport(Library)] internal static partial int noetix_media_set_external_custom_audio_data_to_playback_enable(int enabled);
    [LibraryImport(Library)] internal static partial int noetix_media_get_internal_capture_video_data_to_agent_enable(int* enabled);
    [LibraryImport(Library)] internal static partial int noetix_media_set_internal_capture_video_data_to_agent_enable(int enabled);
    [LibraryImport(Library)] internal static partial int noetix_media_get_external_custom_video_data_to_agent_enable(int* enabled);
    [LibraryImport(Library)] internal static partial int noetix_media_set_external_custom_video_data_to_agent_enable(int enabled);
    [LibraryImport(Library)] internal static partial int noetix_media_get_external_custom_audio_data_to_agent_use_internal_3a(int* enabled);
    [LibraryImport(Library)] internal static partial int noetix_media_set_external_custom_audio_data_to_agent_use_internal_3a(int enabled);

    [LibraryImport(Library)] internal static partial int noetix_media_get_wakeup_response_words(byte* buffer, ulong capacity, ulong* requiredSize);
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)] internal static partial int noetix_media_set_wakeup_response_words(string words);
    [LibraryImport(Library)] internal static partial int noetix_media_get_sleep_response_words(byte* buffer, ulong capacity, ulong* requiredSize);
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)] internal static partial int noetix_media_set_sleep_response_words(string words);
    [LibraryImport(Library)] internal static partial int noetix_media_get_wakeup_words(byte* buffer, ulong capacity, ulong* requiredSize);
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)] internal static partial int noetix_media_add_wakeup_words(string words);
    [LibraryImport(Library)] internal static partial int noetix_media_reset_wakeup_words();

    [LibraryImport(Library)] internal static partial int noetix_media_subscribe_internal_audio_capture(delegate* unmanaged<NativeMediaAudioStream*, nint, void> callback, nint userData);
    [LibraryImport(Library)] internal static partial int noetix_media_subscribe_internal_audio_playback(delegate* unmanaged<NativeMediaAudioStream*, nint, void> callback, nint userData);
    [LibraryImport(Library)] internal static partial int noetix_media_subscribe_internal_video_capture(delegate* unmanaged<NativeMediaVideoStream*, nint, void> callback, nint userData);
    [LibraryImport(Library)] internal static partial int noetix_media_subscribe_internal_video_desensed(delegate* unmanaged<NativeMediaVideoStream*, nint, void> callback, nint userData);

    [LibraryImport(Library)] internal static partial int noetix_media_publish_external_video_stream(NativeMediaVideoStream* stream);
    [LibraryImport(Library)] internal static partial int noetix_media_publish_external_audio_stream(NativeMediaAudioStream* stream);
    [LibraryImport(Library)] internal static partial int noetix_media_publish_external_audio_playback_stream(NativeMediaAudioStream* stream);

    [LibraryImport(Library)] internal static partial int noetix_media_pause_video_capture();
    [LibraryImport(Library)] internal static partial int noetix_media_resume_video_capture();
    [LibraryImport(Library)] internal static partial int noetix_media_pause_audio_capture();
    [LibraryImport(Library)] internal static partial int noetix_media_resume_audio_capture();
    [LibraryImport(Library)] internal static partial int noetix_media_pause_audio_playback();
    [LibraryImport(Library)] internal static partial int noetix_media_resume_audio_playback();
}
