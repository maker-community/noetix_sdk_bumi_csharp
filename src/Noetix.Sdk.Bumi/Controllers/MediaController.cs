using System.Runtime.InteropServices;
using System.Text;
using Noetix.Sdk.Bumi.Native;

namespace Noetix.Sdk.Bumi;

/// <summary>
/// Audio/video/voice-agent control. Mirrors <c>noetix::MediaController</c>, which talks to the
/// robot's voice module over DDS topics.
/// </summary>
/// <remarks>
/// Subscription callbacks run on SDK threads and receive memory that is valid only for the call.
/// Calling a Subscribe method again replaces the previous callback; pass null to remove it.
/// </remarks>
public sealed unsafe class MediaController
{
    private static Action<SystemStatus>? s_systemStatus;
    private static Action<SystemError>? s_systemError;
    private static AudioStreamCallback? s_audioCapture;
    private static AudioStreamCallback? s_audioPlayback;
    private static VideoStreamCallback? s_videoCapture;
    private static VideoStreamCallback? s_videoDesensed;

    public static MediaController Instance { get; } = new();

    private MediaController()
    {
    }

    /// <summary>Calls the official <c>init()</c>; returns false when the SDK reports failure.</summary>
    public bool Init() => Interop.InitSucceeded(NativeMethods.noetix_media_init(), nameof(Init));

    // ---- system control and state

    public void SetSystemControl(SystemControlType type, bool isNeedReply) =>
        Interop.ThrowIfFailed(NativeMethods.noetix_media_set_system_control((int)type, isNeedReply ? 1 : 0), nameof(SetSystemControl));

    public void SubscribeSystemStatus(Action<SystemStatus>? callback)
    {
        Volatile.Write(ref s_systemStatus, callback);
        Interop.ThrowIfFailed(
            callback is null
                ? NativeMethods.noetix_media_subscribe_system_status(null, 0)
                : NativeMethods.noetix_media_subscribe_system_status(&OnSystemStatus, 0),
            nameof(SubscribeSystemStatus));
    }

    public void SubscribeSystemError(Action<SystemError>? callback)
    {
        Volatile.Write(ref s_systemError, callback);
        Interop.ThrowIfFailed(
            callback is null
                ? NativeMethods.noetix_media_subscribe_system_error(null, 0)
                : NativeMethods.noetix_media_subscribe_system_error(&OnSystemError, 0),
            nameof(SubscribeSystemError));
    }

    // ---- volume and timeout

    public int GetVolume()
    {
        int value;
        Interop.ThrowIfFailed(NativeMethods.noetix_media_get_volume(&value), nameof(GetVolume));
        return value;
    }

    public void SetVolume(int value) =>
        Interop.ThrowIfFailed(NativeMethods.noetix_media_set_volume(value), nameof(SetVolume));

    /// <summary>Timeout in milliseconds.</summary>
    public int GetTimeout()
    {
        int value;
        Interop.ThrowIfFailed(NativeMethods.noetix_media_get_timeout(&value), nameof(GetTimeout));
        return value;
    }

    public void SetTimeout(int timeoutMs) =>
        Interop.ThrowIfFailed(NativeMethods.noetix_media_set_timeout(timeoutMs), nameof(SetTimeout));

    // ---- data-flow switches

    public bool GetAudioCueEnable() =>
        GetFlag(&NativeMethods.noetix_media_get_audio_cue_enable, nameof(GetAudioCueEnable));

    public void SetAudioCueEnable(bool enable) =>
        SetFlag(NativeMethods.noetix_media_set_audio_cue_enable, enable, nameof(SetAudioCueEnable));

    /// <summary>Whether the internal microphone audio is sent to the robot's built-in agent.</summary>
    public bool GetInternalCaptureAudioDataToAgentEnable() =>
        GetFlag(&NativeMethods.noetix_media_get_internal_capture_audio_data_to_agent_enable, nameof(GetInternalCaptureAudioDataToAgentEnable));

    public void SetInternalCaptureAudioDataToAgentEnable(bool enable) =>
        SetFlag(NativeMethods.noetix_media_set_internal_capture_audio_data_to_agent_enable, enable, nameof(SetInternalCaptureAudioDataToAgentEnable));

    public bool GetExternalCustomAudioDataToAgentEnable() =>
        GetFlag(&NativeMethods.noetix_media_get_external_custom_audio_data_to_agent_enable, nameof(GetExternalCustomAudioDataToAgentEnable));

    public void SetExternalCustomAudioDataToAgentEnable(bool enable) =>
        SetFlag(NativeMethods.noetix_media_set_external_custom_audio_data_to_agent_enable, enable, nameof(SetExternalCustomAudioDataToAgentEnable));

    /// <summary>Whether the built-in agent's replies are played on the speaker.</summary>
    public bool GetInternalAgentAudioDataToPlaybackEnable() =>
        GetFlag(&NativeMethods.noetix_media_get_internal_agent_audio_data_to_playback_enable, nameof(GetInternalAgentAudioDataToPlaybackEnable));

    public void SetInternalAgentAudioDataToPlaybackEnable(bool enable) =>
        SetFlag(NativeMethods.noetix_media_set_internal_agent_audio_data_to_playback_enable, enable, nameof(SetInternalAgentAudioDataToPlaybackEnable));

    public bool GetExternalCustomAudioDataToPlaybackEnable() =>
        GetFlag(&NativeMethods.noetix_media_get_external_custom_audio_data_to_playback_enable, nameof(GetExternalCustomAudioDataToPlaybackEnable));

    public void SetExternalCustomAudioDataToPlaybackEnable(bool enable) =>
        SetFlag(NativeMethods.noetix_media_set_external_custom_audio_data_to_playback_enable, enable, nameof(SetExternalCustomAudioDataToPlaybackEnable));

    public bool GetInternalCaptureVideoDataToAgentEnable() =>
        GetFlag(&NativeMethods.noetix_media_get_internal_capture_video_data_to_agent_enable, nameof(GetInternalCaptureVideoDataToAgentEnable));

    public void SetInternalCaptureVideoDataToAgentEnable(bool enable) =>
        SetFlag(NativeMethods.noetix_media_set_internal_capture_video_data_to_agent_enable, enable, nameof(SetInternalCaptureVideoDataToAgentEnable));

    public bool GetExternalCustomVideoDataToAgentEnable() =>
        GetFlag(&NativeMethods.noetix_media_get_external_custom_video_data_to_agent_enable, nameof(GetExternalCustomVideoDataToAgentEnable));

    public void SetExternalCustomVideoDataToAgentEnable(bool enable) =>
        SetFlag(NativeMethods.noetix_media_set_external_custom_video_data_to_agent_enable, enable, nameof(SetExternalCustomVideoDataToAgentEnable));

    /// <summary>Whether external audio sent to the agent goes through the robot's internal 3A processing.</summary>
    public bool GetExternalCustomAudioDataToAgentUseInternal3a() =>
        GetFlag(&NativeMethods.noetix_media_get_external_custom_audio_data_to_agent_use_internal_3a, nameof(GetExternalCustomAudioDataToAgentUseInternal3a));

    public void SetExternalCustomAudioDataToAgentUseInternal3a(bool enable) =>
        SetFlag(NativeMethods.noetix_media_set_external_custom_audio_data_to_agent_use_internal_3a, enable, nameof(SetExternalCustomAudioDataToAgentUseInternal3a));

    // ---- wake-up configuration

    public string GetWakeupResponseWords() =>
        Interop.ReadString(NativeMethods.noetix_media_get_wakeup_response_words, nameof(GetWakeupResponseWords));

    public void SetWakeupResponseWords(string words)
    {
        ArgumentNullException.ThrowIfNull(words);
        Interop.ThrowIfFailed(NativeMethods.noetix_media_set_wakeup_response_words(words), nameof(SetWakeupResponseWords));
    }

    public string GetSleepResponseWords() =>
        Interop.ReadString(NativeMethods.noetix_media_get_sleep_response_words, nameof(GetSleepResponseWords));

    public void SetSleepResponseWords(string words)
    {
        ArgumentNullException.ThrowIfNull(words);
        Interop.ThrowIfFailed(NativeMethods.noetix_media_set_sleep_response_words(words), nameof(SetSleepResponseWords));
    }

    public string GetWakeupWords() =>
        Interop.ReadString(NativeMethods.noetix_media_get_wakeup_words, nameof(GetWakeupWords));

    /// <summary>Appends custom wake-up words; returns false when the SDK rejects them.</summary>
    public bool AddWakeupWords(string words)
    {
        ArgumentNullException.ThrowIfNull(words);
        var status = NativeMethods.noetix_media_add_wakeup_words(words);
        if (status == Interop.ErrRejected)
        {
            return false;
        }

        Interop.ThrowIfFailed(status, nameof(AddWakeupWords));
        return true;
    }

    public void ResetWakeupWords() =>
        Interop.ThrowIfFailed(NativeMethods.noetix_media_reset_wakeup_words(), nameof(ResetWakeupWords));

    // ---- audio / video subscriptions

    /// <summary>Audio captured by the robot's internal microphones.</summary>
    public void SubscribeInternalAudioCapture(AudioStreamCallback? callback)
    {
        Volatile.Write(ref s_audioCapture, callback);
        Interop.ThrowIfFailed(
            callback is null
                ? NativeMethods.noetix_media_subscribe_internal_audio_capture(null, 0)
                : NativeMethods.noetix_media_subscribe_internal_audio_capture(&OnAudioCapture, 0),
            nameof(SubscribeInternalAudioCapture));
    }

    /// <summary>Audio the robot is playing on its speaker.</summary>
    public void SubscribeInternalAudioPlayback(AudioStreamCallback? callback)
    {
        Volatile.Write(ref s_audioPlayback, callback);
        Interop.ThrowIfFailed(
            callback is null
                ? NativeMethods.noetix_media_subscribe_internal_audio_playback(null, 0)
                : NativeMethods.noetix_media_subscribe_internal_audio_playback(&OnAudioPlayback, 0),
            nameof(SubscribeInternalAudioPlayback));
    }

    public void SubscribeInternalVideoCapture(VideoStreamCallback? callback)
    {
        Volatile.Write(ref s_videoCapture, callback);
        Interop.ThrowIfFailed(
            callback is null
                ? NativeMethods.noetix_media_subscribe_internal_video_capture(null, 0)
                : NativeMethods.noetix_media_subscribe_internal_video_capture(&OnVideoCapture, 0),
            nameof(SubscribeInternalVideoCapture));
    }

    /// <summary>De-identified video stream.</summary>
    public void SubscribeInternalVideoDesensed(VideoStreamCallback? callback)
    {
        Volatile.Write(ref s_videoDesensed, callback);
        Interop.ThrowIfFailed(
            callback is null
                ? NativeMethods.noetix_media_subscribe_internal_video_desensed(null, 0)
                : NativeMethods.noetix_media_subscribe_internal_video_desensed(&OnVideoDesensed, 0),
            nameof(SubscribeInternalVideoDesensed));
    }

    // ---- external streams

    /// <summary>Publishes video to the robot's voice program.</summary>
    public void PublishExternalVideoStream(in VideoStream stream)
    {
        var sn = Utf8Z(stream.Header.Sn);
        fixed (byte* snPtr = sn)
        fixed (byte* data = stream.VideoData)
        {
            var native = new NativeMediaVideoStream
            {
                Header = new NativeMediaHeader { MessageId = stream.Header.MessageId, TimestampUs = stream.Header.TimestampUs, Sn = snPtr },
                TimestampUs = stream.TimestampUs,
                Format = stream.Format,
                Width = stream.Width,
                Height = stream.Height,
                Fps = stream.Fps,
                Data = data,
                DataSize = (ulong)stream.VideoData.Length,
            };
            Interop.ThrowIfFailed(NativeMethods.noetix_media_publish_external_video_stream(&native), nameof(PublishExternalVideoStream));
        }
    }

    /// <summary>Publishes audio to the robot's voice program (speech recognition / agent).</summary>
    public void PublishExternalAudioStream(in AudioStream stream) =>
        PublishAudio(stream, &NativeMethods.noetix_media_publish_external_audio_stream, nameof(PublishExternalAudioStream));

    /// <summary>Plays audio on the robot's speaker (external_playback topic).</summary>
    public void PublishExternalAudioPlaybackStream(in AudioStream stream) =>
        PublishAudio(stream, &NativeMethods.noetix_media_publish_external_audio_playback_stream, nameof(PublishExternalAudioPlaybackStream));

    // ---- capture / playback control

    public void PauseVideoCapture() => Interop.ThrowIfFailed(NativeMethods.noetix_media_pause_video_capture(), nameof(PauseVideoCapture));

    public void ResumeVideoCapture() => Interop.ThrowIfFailed(NativeMethods.noetix_media_resume_video_capture(), nameof(ResumeVideoCapture));

    public void PauseAudioCapture() => Interop.ThrowIfFailed(NativeMethods.noetix_media_pause_audio_capture(), nameof(PauseAudioCapture));

    public void ResumeAudioCapture() => Interop.ThrowIfFailed(NativeMethods.noetix_media_resume_audio_capture(), nameof(ResumeAudioCapture));

    public void PauseAudioPlayback() => Interop.ThrowIfFailed(NativeMethods.noetix_media_pause_audio_playback(), nameof(PauseAudioPlayback));

    public void ResumeAudioPlayback() => Interop.ThrowIfFailed(NativeMethods.noetix_media_resume_audio_playback(), nameof(ResumeAudioPlayback));

    // ---- helpers

    private static bool GetFlag(delegate*<int*, int> getter, string operation)
    {
        int value;
        Interop.ThrowIfFailed(getter(&value), operation);
        return value != 0;
    }

    private static void SetFlag(Func<int, int> setter, bool enable, string operation) =>
        Interop.ThrowIfFailed(setter(enable ? 1 : 0), operation);

    private static byte[] Utf8Z(string? value) => Encoding.UTF8.GetBytes((value ?? string.Empty) + '\0');

    private static void PublishAudio(in AudioStream stream, delegate*<NativeMediaAudioStream*, int> publish, string operation)
    {
        var sn = Utf8Z(stream.Header.Sn);
        fixed (byte* snPtr = sn)
        fixed (short* samples = stream.AudioData)
        {
            var native = new NativeMediaAudioStream
            {
                Header = new NativeMediaHeader { MessageId = stream.Header.MessageId, TimestampUs = stream.Header.TimestampUs, Sn = snPtr },
                TimestampUs = stream.TimestampUs,
                Channels = stream.Channels,
                SampleRate = stream.SampleRate,
                Format = stream.Format,
                DurationMs = stream.DurationMs,
                Samples = samples,
                SampleCount = (ulong)stream.AudioData.Length,
            };
            Interop.ThrowIfFailed(publish(&native), operation);
        }
    }

    private static MediaHeader ToManaged(in NativeMediaHeader h) =>
        new(h.MessageId, h.TimestampUs, Interop.PtrToString(h.Sn));

    private static AudioStream ToManaged(NativeMediaAudioStream* s) => new()
    {
        Header = ToManaged(in s->Header),
        TimestampUs = s->TimestampUs,
        Channels = s->Channels,
        SampleRate = s->SampleRate,
        Format = s->Format,
        DurationMs = s->DurationMs,
        AudioData = new ReadOnlySpan<short>(s->Samples, checked((int)s->SampleCount)),
    };

    private static VideoStream ToManaged(NativeMediaVideoStream* s) => new()
    {
        Header = ToManaged(in s->Header),
        TimestampUs = s->TimestampUs,
        Format = s->Format,
        Width = s->Width,
        Height = s->Height,
        Fps = s->Fps,
        VideoData = new ReadOnlySpan<byte>(s->Data, checked((int)s->DataSize)),
    };

    // ---- native callbacks (SDK threads; must never throw)

    [UnmanagedCallersOnly]
    private static void OnSystemStatus(NativeMediaSystemStatus* s, nint userData)
    {
        var callback = Volatile.Read(ref s_systemStatus);
        if (callback is null)
        {
            return;
        }

        try
        {
            callback(new SystemStatus(ToManaged(in s->Header), (WorkStatus)s->Value, (StatusChangeReason)s->Reason));
        }
        catch (Exception ex)
        {
            NoetixSdk.ReportCallbackError(ex);
        }
    }

    [UnmanagedCallersOnly]
    private static void OnSystemError(NativeMediaSystemError* e, nint userData)
    {
        var callback = Volatile.Read(ref s_systemError);
        if (callback is null)
        {
            return;
        }

        try
        {
            callback(new SystemError(ToManaged(in e->Header), e->Code, Interop.PtrToString(e->Message)));
        }
        catch (Exception ex)
        {
            NoetixSdk.ReportCallbackError(ex);
        }
    }

    [UnmanagedCallersOnly]
    private static void OnAudioCapture(NativeMediaAudioStream* s, nint userData) =>
        InvokeAudio(Volatile.Read(ref s_audioCapture), s);

    [UnmanagedCallersOnly]
    private static void OnAudioPlayback(NativeMediaAudioStream* s, nint userData) =>
        InvokeAudio(Volatile.Read(ref s_audioPlayback), s);

    [UnmanagedCallersOnly]
    private static void OnVideoCapture(NativeMediaVideoStream* s, nint userData) =>
        InvokeVideo(Volatile.Read(ref s_videoCapture), s);

    [UnmanagedCallersOnly]
    private static void OnVideoDesensed(NativeMediaVideoStream* s, nint userData) =>
        InvokeVideo(Volatile.Read(ref s_videoDesensed), s);

    private static void InvokeAudio(AudioStreamCallback? callback, NativeMediaAudioStream* s)
    {
        if (callback is null)
        {
            return;
        }

        try
        {
            var stream = ToManaged(s);
            callback(in stream);
        }
        catch (Exception ex)
        {
            NoetixSdk.ReportCallbackError(ex);
        }
    }

    private static void InvokeVideo(VideoStreamCallback? callback, NativeMediaVideoStream* s)
    {
        if (callback is null)
        {
            return;
        }

        try
        {
            var stream = ToManaged(s);
            callback(in stream);
        }
        catch (Exception ex)
        {
            NoetixSdk.ReportCallbackError(ex);
        }
    }
}
