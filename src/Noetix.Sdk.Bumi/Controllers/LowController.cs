using System.Runtime.InteropServices;
using Noetix.Sdk.Bumi.Native;

namespace Noetix.Sdk.Bumi;

/// <summary>
/// Motor-level control (EDU models only). Mirrors <c>noetix::LowController</c>: the SDK keeps the
/// latest command array and sends it to the motion controller every 2 ms.
/// </summary>
/// <remarks>
/// A bad policy can damage the robot or hurt people. Use a gantry while developing and respect the
/// joint limits in the official documentation. Managed code is not suited to hard-real-time loops;
/// prefer the official C++ path for 500 Hz policies.
/// </remarks>
public sealed unsafe class LowController
{
    private static RobotHardwareStatusCallback? s_statusCallback;

    public static LowController Instance { get; } = new();

    private LowController()
    {
    }

    /// <summary>Calls the official <c>init()</c>; returns false when the SDK reports failure.</summary>
    public bool Init() => Interop.InitSucceeded(NativeLowMethods.noetix_low_init(), nameof(Init), lowLibrary: true);

    /// <summary>Stores the commands for the SDK's send thread. Must contain exactly 21 entries.</summary>
    public void SetJoint(ReadOnlySpan<MotorCmd> motorCmd)
    {
        if (motorCmd.Length != RobotHardwareStatus.MotorCount)
        {
            throw new ArgumentException($"Exactly {RobotHardwareStatus.MotorCount} motor commands are required.", nameof(motorCmd));
        }

        fixed (MotorCmd* p = motorCmd)
        {
            Interop.ThrowIfFailed(NativeLowMethods.noetix_low_set_joint(p, (uint)motorCmd.Length), nameof(SetJoint), lowLibrary: true);
        }
    }

    /// <summary>
    /// Installs the status callback, invoked on an SDK thread; keep it short. Calling again replaces
    /// the previous callback; pass null to remove it.
    /// </summary>
    public void SubscribeRobotHardwareStatus(RobotHardwareStatusCallback? callback)
    {
        Volatile.Write(ref s_statusCallback, callback);
        var status = callback is null
            ? NativeLowMethods.noetix_low_subscribe_robot_hardware_status(null, 0)
            : NativeLowMethods.noetix_low_subscribe_robot_hardware_status(&OnStatus, 0);
        Interop.ThrowIfFailed(status, nameof(SubscribeRobotHardwareStatus), lowLibrary: true);
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
