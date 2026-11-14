using System.Runtime.InteropServices;
using Noetix.Sdk.Bumi.Native;
using Xunit;

namespace Noetix.Sdk.Bumi.Tests;

/// <summary>Runs only when NOETIX_BUMI_NATIVE_TESTS=1 and the native libraries are loadable (Linux).</summary>
public sealed class FactIfNativeAttribute : FactAttribute
{
    public FactIfNativeAttribute()
    {
        if (Environment.GetEnvironmentVariable("NOETIX_BUMI_NATIVE_TESTS") != "1")
        {
            Skip = "Set NOETIX_BUMI_NATIVE_TESTS=1 with libnoetix_bumi_c.so on the library path.";
        }
    }
}

// None of these tests need a robot: they only exercise paths that do not touch DDS.
public class NativeSmokeTests
{
    [DllImport("libc", EntryPoint = "getenv")]
    private static extern nint GetEnv([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [FactIfNative]
    public void Native_library_reports_expected_versions()
    {
        Assert.Equal(NoetixSdk.ExpectedSdkVersion, NoetixSdk.NativeSdkVersion);
        Assert.Equal(1, NoetixSdk.NativeApiVersion);
    }

    [FactIfNative]
    public void SetDdsConfig_is_visible_to_native_code()
    {
        var path = Path.Combine(Path.GetTempPath(), "dds-test.xml");
        NoetixSdk.SetDdsConfig(path);

        var value = Marshal.PtrToStringUTF8(GetEnv("CYCLONEDDS_URI"));
        Assert.Equal("file://" + Path.GetFullPath(path), value);
    }

    [FactIfNative]
    public void PublishCmd_rejects_out_of_range_values_with_native_message()
    {
        var ex = Assert.Throws<NoetixSdkException>(() => HighController.Instance.PublishCmd(2.0, 0, 0, ControlCmd.Walk));
        Assert.Equal(-2, ex.StatusCode);
        Assert.Contains("[-1, 1]", ex.Message);
    }

    [FactIfNative]
    public void PublishCmd_rejects_non_finite_values()
    {
        var ex = Assert.Throws<NoetixSdkException>(() => HighController.Instance.PublishCmd(0, double.NaN, 0, ControlCmd.Walk));
        Assert.Equal(-2, ex.StatusCode);
    }

    [FactIfNative]
    public void PublishCmd_rejects_unknown_action()
    {
        var ex = Assert.Throws<NoetixSdkException>(() => HighController.Instance.PublishCmd(0, 0, 0, (ControlCmd)999));
        Assert.Equal(-2, ex.StatusCode);
    }

    [FactIfNative]
    public unsafe void Media_argument_validation_reports_through_native_layer()
    {
        var status = NativeMethods.noetix_media_get_volume(null);
        Assert.Equal(-2, status);
        Assert.Contains("value", Interop.Create(status, "test").Message);
    }

    [FactIfNative]
    public unsafe void Low_library_loads_separately_and_reports_its_own_errors()
    {
        var status = NativeLowMethods.noetix_low_set_joint(null, 21);
        Assert.Equal(-2, status);
        Assert.Contains("commands", Interop.Create(status, "test", lowLibrary: true).Message);
    }
}
