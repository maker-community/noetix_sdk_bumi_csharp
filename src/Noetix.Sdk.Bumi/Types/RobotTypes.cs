using System.Runtime.InteropServices;

namespace Noetix.Sdk.Bumi;

// Layouts mirror native/include/noetix_bumi_c.h, which mirrors the official
// noetix::common.h. Tests assert the sizes.

[StructLayout(LayoutKind.Sequential)]
public struct MotorState
{
    public double Pos;
    public double Vel;
    public double Tau;
    public ushort MotorId;
    public byte Error;
    public int Temperature;
}

/// <summary>Motor command. <see cref="Pos"/> is in radians.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct MotorCmd
{
    public double Pos;
    public double Vel;
    public double Tau;
    public double Kp;
    public double Kd;
    public ushort MotorId;
}

[StructLayout(LayoutKind.Sequential)]
public struct ImuData
{
    public DoubleArray4 Ori;
    public DoubleArray9 OriCov;
    public DoubleArray3 AngularVel;
    public DoubleArray9 AngularVelCov;
    public DoubleArray3 LinearAcc;
    public DoubleArray9 LinearAccCov;
}

[StructLayout(LayoutKind.Sequential)]
public struct JoyData
{
    public DoubleArray2 Axes;
    public Int32Array14 Button;
}

[StructLayout(LayoutKind.Sequential)]
public struct BmsData
{
    public ushort BatteryTemp;
    public ushort BatteryAlarm;
    public ushort BatterySoc;
    public ushort BatterySoh;
}

[StructLayout(LayoutKind.Sequential)]
public struct RobotHardwareStatus
{
    public const int MotorCount = 21;

    public ImuData ImuData;
    public JoyData RemoteData;
    public MotorStateArray MotorData;
    public BmsData BmsData;

    /// <summary>Current work mode; only meaningful under HighController (see the official docs).</summary>
    public int WorkMode;
}
