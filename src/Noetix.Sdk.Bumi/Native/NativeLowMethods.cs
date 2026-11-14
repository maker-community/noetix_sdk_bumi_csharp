using System.Runtime.InteropServices;

namespace Noetix.Sdk.Bumi.Native;

// Functions from native/include/noetix_bumi_low_c.h. LowController lives in its own native library
// because the official High and Low controllers cannot be linked together.
internal static unsafe partial class NativeLowMethods
{
    internal const string Library = "noetix_bumi_low_c";

    [LibraryImport(Library)] internal static partial byte* noetix_bumi_last_error();

    [LibraryImport(Library)] internal static partial int noetix_low_init();
    [LibraryImport(Library)] internal static partial int noetix_low_set_joint(MotorCmd* commands, uint count);
    [LibraryImport(Library)] internal static partial int noetix_low_subscribe_robot_hardware_status(delegate* unmanaged<RobotHardwareStatus*, nint, void> callback, nint userData);
}
