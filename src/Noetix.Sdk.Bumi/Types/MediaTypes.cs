namespace Noetix.Sdk.Bumi;

/// <summary>Mirrors <c>noetix::media::WorkStatus</c>.</summary>
public enum WorkStatus
{
    Ready = 0,
    Sleeped = 1,
    Wakeuped = 2,
    Exit = 3,
}

/// <summary>Mirrors <c>noetix::media::StatusChangeReason</c>.</summary>
public enum StatusChangeReason
{
    SystemLaunch = 0,
    CmdReset = 1,
    AudioWakeuped = 2,
    CmdWakeuped = 3,
    AudioSleeped = 4,
    CmdSleeped = 5,
    TimeoutSleeped = 6,
    ErrorSleeped = 7,
}

/// <summary>Mirrors <c>noetix::media::SystemControlType</c>.</summary>
public enum SystemControlType
{
    ToWakeup = 0,
    ToSleep = 1,
    ToReset = 2,
}

public readonly record struct MediaHeader(ulong MessageId, ulong TimestampUs, string Sn);

public readonly record struct SystemStatus(MediaHeader Header, WorkStatus Value, StatusChangeReason Reason);

public readonly record struct SystemError(MediaHeader Header, int Code, string Message);

/// <summary>
/// Audio frame. When received in a callback, <see cref="AudioData"/> points at
/// SDK memory that is only valid during the callback; copy it to keep it.
/// </summary>
public readonly ref struct AudioStream
{
    public MediaHeader Header { get; init; }
    public ulong TimestampUs { get; init; }
    public uint Channels { get; init; }
    public uint SampleRate { get; init; }
    public uint Format { get; init; }
    public uint DurationMs { get; init; }
    public ReadOnlySpan<short> AudioData { get; init; }
}

/// <summary>
/// Video frame. When received in a callback, <see cref="VideoData"/> points at
/// SDK memory that is only valid during the callback; copy it to keep it.
/// </summary>
public readonly ref struct VideoStream
{
    public MediaHeader Header { get; init; }
    public ulong TimestampUs { get; init; }
    public uint Format { get; init; }
    public uint Width { get; init; }
    public uint Height { get; init; }
    public uint Fps { get; init; }
    public ReadOnlySpan<byte> VideoData { get; init; }
}

public delegate void RobotHardwareStatusCallback(in RobotHardwareStatus status);

public delegate void AudioStreamCallback(in AudioStream stream);

public delegate void VideoStreamCallback(in VideoStream stream);
