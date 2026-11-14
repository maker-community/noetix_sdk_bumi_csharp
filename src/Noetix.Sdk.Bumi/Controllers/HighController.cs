using System.Runtime.InteropServices;
using Noetix.Sdk.Bumi.Native;

namespace Noetix.Sdk.Bumi;

/// <summary>
/// High-level control: the factory gait and balance keep running and the application only
/// sends velocity commands and actions. Mirrors <c>noetix::HighController</c>.
/// </summary>
/// <remarks>
/// The official SDK expects commands to be published continuously (its examples loop at ~100 Hz,
/// sending <see cref="ControlCmd.Default"/> when idle) and asks for at least 2 ms between calls.
/// This binding does not throttle; that is the caller's responsibility.
/// </remarks>
public sealed unsafe class HighController
{
    private static RobotHardwareStatusCallback? s_statusCallback;

    public static HighController Instance { get; } = new();

    private HighController()
    {
    }

    /// <summary>
    /// Calls the official <c>init()</c>; returns false when the SDK reports failure.
    /// Observed with no robot reachable: the call blocks (still blocked after 60 s), so run it
    /// off the UI thread and give it a timeout.
    /// </summary>
    public bool Init() => Interop.InitSucceeded(NativeMethods.noetix_high_init(), nameof(Init));

    /// <param name="x">Forward (+) / backward (-), within [-1, 1].</param>
    /// <param name="y">Left (+) / right (-), within [-1, 1].</param>
    /// <param name="z">Turn left (+) / right (-), within [-1, 1].</param>
    /// <param name="action">Action, or <see cref="ControlCmd.Default"/> for none.</param>
    /// <param name="index">Teach-file index for the teach actions.</param>
    /// <exception cref="NoetixSdkException">An argument is non-finite or out of range, or the SDK failed.</exception>
    public void PublishCmd(double x, double y, double z, ControlCmd action, ushort index = 0) =>
        Interop.ThrowIfFailed(NativeMethods.noetix_high_publish_cmd(x, y, z, (uint)action, index), nameof(PublishCmd));

    /// <summary>
    /// Installs the status callback, invoked on an SDK thread; keep it short. Calling again replaces
    /// the previous callback; pass null to remove it.
    /// </summary>
    public void SubscribeRobotHardwareStatus(RobotHardwareStatusCallback? callback)
    {
        Volatile.Write(ref s_statusCallback, callback);
        var status = callback is null
            ? NativeMethods.noetix_high_subscribe_robot_hardware_status(null, 0)
            : NativeMethods.noetix_high_subscribe_robot_hardware_status(&OnStatus, 0);
        Interop.ThrowIfFailed(status, nameof(SubscribeRobotHardwareStatus));
    }

    [UnmanagedCallersOnly]
    private static void OnStatus(RobotHardwareStatus* status, nint userData)
    {
        var callback = Volatile.Read(ref s_statusCallback);
        if (callback is null)
        {
            return;
        }

        try
        {
            callback(in *status);
        }
        catch (Exception ex)
        {
            NoetixSdk.ReportCallbackError(ex);
        }
    }
}
