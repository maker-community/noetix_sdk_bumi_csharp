using Noetix.Sdk.Bumi;

// Read-only smoke test: connects to the robot, prints state for a few seconds, never sends motion.
// Usage: StatusMonitor [path-to-dds.xml] [seconds]
var ddsConfig = args.Length > 0 ? args[0] : Path.Combine("config", "dds.xml");
var seconds = args.Length > 1 && int.TryParse(args[1], out var s) ? s : 10;

Console.WriteLine($"Native SDK version: {NoetixSdk.NativeSdkVersion} (bindings written for {NoetixSdk.ExpectedSdkVersion})");

NoetixSdk.CallbackError += (_, ex) => Console.Error.WriteLine($"callback error: {ex}");
NoetixSdk.SetDdsConfig(ddsConfig);

var high = HighController.Instance;
if (!high.Init())
{
    Console.Error.WriteLine("HighController.Init() returned false.");
    return 1;
}

var count = 0;
high.SubscribeRobotHardwareStatus((in RobotHardwareStatus status) =>
{
    if (Interlocked.Increment(ref count) % 100 != 1)
    {
        return;
    }

    Console.WriteLine(
        $"workmode={status.WorkMode} soc={status.BmsData.BatterySoc}% alarm={status.BmsData.BatteryAlarm} " +
        $"quat=({status.ImuData.Ori[0]:F3},{status.ImuData.Ori[1]:F3},{status.ImuData.Ori[2]:F3},{status.ImuData.Ori[3]:F3})");
});

await Task.Delay(TimeSpan.FromSeconds(seconds));
high.SubscribeRobotHardwareStatus(null);
Console.WriteLine($"Received {count} status messages.");
return count > 0 ? 0 : 2;
