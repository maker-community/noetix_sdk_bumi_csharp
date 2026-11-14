using System.Runtime.InteropServices;

namespace Noetix.Sdk.Bumi.Native;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NativeMediaHeader
{
    public ulong MessageId;
    public ulong TimestampUs;
    public byte* Sn;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeMediaSystemStatus
{
    public NativeMediaHeader Header;
    public int Value;
    public int Reason;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NativeMediaSystemError
{
    public NativeMediaHeader Header;
    public int Code;
    public byte* Message;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NativeMediaAudioStream
{
    public NativeMediaHeader Header;
    public ulong TimestampUs;
    public uint Channels;
    public uint SampleRate;
    public uint Format;
    public uint DurationMs;
    public short* Samples;
    public ulong SampleCount;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NativeMediaVideoStream
{
    public NativeMediaHeader Header;
    public ulong TimestampUs;
    public uint Format;
    public uint Width;
    public uint Height;
    public uint Fps;
    public byte* Data;
    public ulong DataSize;
}
