using System.Runtime.InteropServices;
using System.Text;
using Noetix.Sdk.Bumi.Native;

namespace Noetix.Sdk.Bumi;

/// <summary>Raised when a call into the native SDK fails.</summary>
public sealed class NoetixSdkException : Exception
{
    public int StatusCode { get; }

    public NoetixSdkException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}

/// <summary>Library-level helpers: DDS configuration, versions and callback diagnostics.</summary>
public static unsafe class NoetixSdk
{
    /// <summary>Official SDK version these bindings were written against.</summary>
    public const string ExpectedSdkVersion = "3.1.0";

    /// <summary>Raised when a user callback throws; exceptions never propagate into native threads.</summary>
    public static event EventHandler<Exception>? CallbackError;

    /// <summary>SDK_VERSION of the official headers the native library was built with.</summary>
    public static string NativeSdkVersion =>
        Marshal.PtrToStringUTF8((nint)NativeMethods.noetix_bumi_sdk_version()) ?? string.Empty;

    public static int NativeApiVersion => NativeMethods.noetix_bumi_c_api_version();

    /// <summary>
    /// Points CycloneDDS at an XML file (the official <c>config/dds.xml</c>). Call before any
    /// <c>Init()</c>. <see cref="Environment.SetEnvironmentVariable(string, string)"/> is not
    /// sufficient on Linux because native code does not see it.
    /// </summary>
    public static void SetDdsConfig(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        Interop.ThrowIfFailed(NativeMethods.noetix_bumi_set_dds_config(Path.GetFullPath(path)), nameof(SetDdsConfig));
    }

    internal static void ReportCallbackError(Exception exception)
    {
        try
        {
            CallbackError?.Invoke(null, exception);
        }
        catch
        {
            // Nothing sensible can be done on a native thread.
        }
    }
}

internal static unsafe class Interop
{
    internal const int Ok = 0;
    internal const int ErrInitFailed = -3;
    internal const int ErrRejected = -4;

    internal delegate int StringGetter(byte* buffer, ulong capacity, ulong* requiredSize);

    internal static void ThrowIfFailed(int status, string operation, bool lowLibrary = false)
    {
        if (status != Ok)
        {
            throw Create(status, operation, lowLibrary);
        }
    }

    /// <summary>Maps an init status to the official bool result; other failures throw.</summary>
    internal static bool InitSucceeded(int status, string operation, bool lowLibrary = false) => status switch
    {
        Ok => true,
        ErrInitFailed => false,
        _ => throw Create(status, operation, lowLibrary),
    };

    internal static NoetixSdkException Create(int status, string operation, bool lowLibrary = false)
    {
        // The message is thread-local inside the library that reported the failure.
        var detail = Marshal.PtrToStringUTF8(
            (nint)(lowLibrary ? NativeLowMethods.noetix_bumi_last_error() : NativeMethods.noetix_bumi_last_error()));
        var message = string.IsNullOrEmpty(detail)
            ? $"{operation} failed with status {status}."
            : $"{operation} failed with status {status}: {detail}";
        return new NoetixSdkException(status, message);
    }

    internal static string ReadString(StringGetter getter, string operation)
    {
        Span<byte> stack = stackalloc byte[256];
        ulong required;
        fixed (byte* p = stack)
        {
            ThrowIfFailed(getter(p, (ulong)stack.Length, &required), operation);
            if (required <= (ulong)stack.Length)
            {
                return Encoding.UTF8.GetString(p, (int)required - 1);
            }
        }

        var heap = new byte[required];
        fixed (byte* p = heap)
        {
            ThrowIfFailed(getter(p, required, &required), operation);
            return Encoding.UTF8.GetString(p, (int)Math.Min(required, (ulong)heap.Length) - 1);
        }
    }

    internal static string PtrToString(byte* p) => Marshal.PtrToStringUTF8((nint)p) ?? string.Empty;
}
