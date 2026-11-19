using Noetix.Sdk.Bumi;

// Hardware motion-path check. Streams PublishCmd continuously at the official rate (the vendor
// examples never stop publishing), sends each requested action once inside that stream, and reports
// the observed workmode sequence plus the largest joint deviation as evidence that the action
// reached the robot.
// Usage: MotionTest [path-to-dds.xml] [probe|action|Action1>Action2] [seconds] [hz]
// Run only with the robot on a gantry, the area cleared and an emergency stop at hand.
var ddsConfig = args.Length > 0 ? args[0] : Path.Combine("config", "dds.xml");
var spec = args.Length > 1 ? args[1] : nameof(ControlCmd.Swing);
var seconds = args.Length > 2 && double.TryParse(args[2], out var s) ? s : 10;
var hz = args.Length > 3 && int.TryParse(args[3], out var h) && h is >= 1 and <= 500 ? h : 500;

// Official workmode table (Bumi document center): 0 enabled, 1 ready, 2 walking, 8/9/10 preset
// wave/handshake/cheer, 26 protection, 30 disabled.
const int WorkModeProtection = 26;
const int WorkModeDisabled = 30;

// Official usage: the vendor examples publish at 100 Hz (Python) / 500 Hz (C++) and ask for at
// least 2 ms between calls, sending ControlCmd.Default when idle.
var probeOnly = spec.Equals("probe", StringComparison.OrdinalIgnoreCase);
var actions = new List<ControlCmd>();
if (!probeOnly)
{
    foreach (var part in spec.Split('>', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        if (!Enum.TryParse(part, ignoreCase: true, out ControlCmd parsed))
        {
            Console.Error.WriteLine($"Unknown action '{part}'. Known: {string.Join(", ", Enum.GetNames<ControlCmd>())}, probe");
            return 64;
        }

        actions.Add(parsed);
    }

    if (actions.Count == 0)
    {
        Console.Error.WriteLine($"No action in '{spec}'.");
        return 64;
    }
}

Console.WriteLine($"Native SDK version: {NoetixSdk.NativeSdkVersion}");
Console.WriteLine(probeOnly
    ? $"Probe only: no commands will be published. Duration: {seconds}s"
    : $"Actions: {string.Join(" -> ", actions)} (each sent once); streaming {ControlCmd.Default} otherwise at {hz} Hz; duration: {seconds}s");

NoetixSdk.CallbackError += (_, ex) => Console.Error.WriteLine($"callback error: {ex}");
NoetixSdk.SetDdsConfig(ddsConfig);

var high = HighController.Instance;
if (!high.Init())
{
    Console.Error.WriteLine("HighController.Init() returned false.");
    return 1;
}

const int MotorCount = RobotHardwareStatus.MotorCount;

// Official joint order ("关节电机信息" in the Bumi document center): 0-3 left arm, 4-9 left leg,
// 10-13 right arm, 14-19 right leg, 20 waist.
static string JointName(int id) => id switch
{
    0 => "arm_l1 shoulder pitch",
    1 => "arm_l2 shoulder roll",
    2 => "arm_l3 upper-arm yaw",
    3 => "arm_l4 elbow pitch",
    4 => "leg_l1 hip pitch",
    5 => "leg_l2 hip roll",
    6 => "leg_l3 thigh yaw",
    7 => "leg_l4 knee pitch",
    8 => "leg_l5 ankle pitch",
    9 => "leg_l6 ankle roll",
    10 => "arm_r1 shoulder pitch",
    11 => "arm_r2 shoulder roll",
    12 => "arm_r3 upper-arm yaw",
    13 => "arm_r4 elbow pitch",
    14 => "leg_r1 hip pitch",
    15 => "leg_r2 hip roll",
    16 => "leg_r3 thigh yaw",
    17 => "leg_r4 knee pitch",
    18 => "leg_r5 ankle pitch",
    19 => "leg_r6 ankle roll",
    20 => "waist yaw",
    _ => $"joint[{id}]",
};

static string Region(int id) => id switch
{
    < 4 => "arm",
    < 8 => "leg",
    < 10 => "foot",
    < 14 => "arm",
    < 18 => "leg",
    < 20 => "foot",
    _ => "waist",
};

var gate = new object();
var statusCount = 0;
var workModes = new List<int>();
var lastWorkMode = int.MinValue;
var sentActions = 0;
var sentDefaults = 0;
var lastPos = new double[MotorCount];
var lastTau = new double[MotorCount];
var lastError = new byte[MotorCount];
var motorIds = new ushort[MotorCount];
var haveSample = false;
var tracking = false;
var baseline = new double[MotorCount];
var posMin = new double[MotorCount];
var posMax = new double[MotorCount];

high.SubscribeRobotHardwareStatus((in RobotHardwareStatus status) =>
{
    lock (gate)
    {
        statusCount++;
        if (status.WorkMode != lastWorkMode)
        {
            lastWorkMode = status.WorkMode;
            workModes.Add(status.WorkMode);
        }

        for (var i = 0; i < MotorCount; i++)
        {
            var pos = status.MotorData[i].Pos;
            motorIds[i] = status.MotorData[i].MotorId;
            lastPos[i] = pos;
            lastTau[i] = status.MotorData[i].Tau;
            lastError[i] = status.MotorData[i].Error;

            if (tracking)
            {
                posMin[i] = Math.Min(posMin[i], pos);
                posMax[i] = Math.Max(posMax[i], pos);
            }
        }

        haveSample = true;
    }
});

int WorkModeNow()
{
    lock (gate)
    {
        return lastWorkMode;
    }
}

bool CaptureBaseline(string label)
{
    lock (gate)
    {
        if (!haveSample)
        {
            return false;
        }

        for (var i = 0; i < MotorCount; i++)
        {
            baseline[i] = lastPos[i];
            posMin[i] = baseline[i];
            posMax[i] = baseline[i];
        }

        tracking = true;
        Console.WriteLine($"Baseline '{label}' after {statusCount} status messages; workmode={lastWorkMode}.");
        return true;
    }
}

var firstStatusDeadline = DateTime.UtcNow.AddSeconds(3);
while (DateTime.UtcNow < firstStatusDeadline && !haveSample)
{
    await Task.Delay(50);
}

if (!haveSample)
{
    high.SubscribeRobotHardwareStatus(null);
    Console.Error.WriteLine("Received 0 status messages; aborting without publishing.");
    return 2;
}

if (probeOnly)
{
    await Task.Delay(TimeSpan.FromSeconds(seconds));
    high.SubscribeRobotHardwareStatus(null);
    Report();
    return statusCount > 0 ? 0 : 2;
}

CaptureBaseline("initial");
var modeBeforeActions = WorkModeNow();
if (modeBeforeActions is WorkModeDisabled or WorkModeProtection)
{
    high.SubscribeRobotHardwareStatus(null);
    Console.Error.WriteLine(
        $"workmode={modeBeforeActions}; the robot is disabled or in protection, so it would ignore " +
        $"{string.Join("/", actions)}. Send {ControlCmd.Start} first (from workmode 30 that enables it). " +
        "Nothing was published.");
    return 4;
}

// Official idle behaviour: publish Default continuously, and put the action samples inside that
// uninterrupted stream. The earlier attempts sent an action and then went silent, which is not how
// the vendor examples drive the robot.
var period = TimeSpan.FromSeconds(1.0 / hz);
var startedAt = DateTime.UtcNow;
var stopAt = startedAt.AddSeconds(seconds);
var firstActionAt = startedAt.AddSeconds(1);      // let the Default stream settle first
var actionGap = TimeSpan.FromSeconds(4);          // official mode switches are not instantaneous
var schedule = new List<(DateTime At, ControlCmd Action)>();
for (var i = 0; i < actions.Count; i++)
{
    schedule.Add((firstActionAt + (actionGap * i), actions[i]));
}

var failure = 0;
var nextAction = 0;
try
{
    while (DateTime.UtcNow < stopAt)
    {
        if (nextAction < schedule.Count && DateTime.UtcNow >= schedule[nextAction].At)
        {
            var (_, action) = schedule[nextAction];
            var modeBefore = WorkModeNow();
            high.PublishCmd(0, 0, 0, action);
            sentActions++;
            nextAction++;
            Console.WriteLine(
                $"[{(DateTime.UtcNow - startedAt).TotalSeconds,5:F1}s] sent {action} once; " +
                $"workmode before={modeBefore}, after={WorkModeNow()}");
        }
        else
        {
            high.PublishCmd(0, 0, 0, ControlCmd.Default);
            sentDefaults++;
        }

        await Task.Delay(period);
    }
}
catch (NoetixSdkException ex)
{
    failure = 3;
    Console.Error.WriteLine($"PublishCmd failed after {sentActions} actions and {sentDefaults} idle commands: {ex.Message}");
}

await Task.Delay(TimeSpan.FromSeconds(1));
high.SubscribeRobotHardwareStatus(null);

if (nextAction < schedule.Count)
{
    Console.Error.WriteLine(
        $"Only {nextAction}/{schedule.Count} actions were sent in {seconds}s; " +
        $"raise the duration above {Math.Ceiling(1 + (actionGap.TotalSeconds * schedule.Count))}s.");
    failure = failure == 0 ? 5 : failure;
}

Report();
return failure;

void Report()
{
    int[] workModeHistory;
    double[] deltas;
    double[] tauSnapshot;
    byte[] errorSnapshot;
    double[] posSnapshot;
    ushort[] idSnapshot;
    var lastWorkModeSeen = 0;
    var errorMotors = 0;
    var meanAbsTau = 0.0;
    lock (gate)
    {
        workModeHistory = workModes.ToArray();
        lastWorkModeSeen = lastWorkMode;
        tauSnapshot = (double[])lastTau.Clone();
        errorSnapshot = (byte[])lastError.Clone();
        posSnapshot = (double[])lastPos.Clone();
        idSnapshot = (ushort[])motorIds.Clone();
        deltas = new double[MotorCount];
        for (var i = 0; i < MotorCount; i++)
        {
            deltas[i] = Math.Max(Math.Abs(posMin[i] - baseline[i]), Math.Abs(posMax[i] - baseline[i]));
            meanAbsTau += Math.Abs(lastTau[i]);
            errorMotors += lastError[i] != 0 ? 1 : 0;
        }

        meanAbsTau /= MotorCount;
    }

    Console.WriteLine($"Work modes observed: {(workModeHistory.Length == 0 ? "none" : string.Join(" -> ", workModeHistory))}");
    Console.WriteLine($"Final workmode={lastWorkModeSeen}, motors_with_error={errorMotors}, mean|tau|={meanAbsTau:F2}");
    Console.WriteLine(probeOnly
        ? "Published 0 commands (probe only)."
        : $"Published {sentActions} action command(s) and {sentDefaults} {ControlCmd.Default} commands.");

    if (probeOnly)
    {
        Console.WriteLine("index id joint error tau(Nm) pos(rad)");
        for (var i = 0; i < MotorCount; i++)
        {
            Console.WriteLine($"  [{i,2}] id={idSnapshot[i],2} {JointName(i),-22} error={errorSnapshot[i],3} tau={tauSnapshot[i],8:F2} pos={posSnapshot[i],8:F3}");
        }
    }

    Console.WriteLine($"Received {statusCount} status messages.");

    var armMax = 0.0;
    var legMax = 0.0;
    var footMax = 0.0;
    var waistMax = 0.0;
    for (var i = 0; i < MotorCount; i++)
    {
        switch (Region(i))
        {
            case "arm": armMax = Math.Max(armMax, deltas[i]); break;
            case "leg": legMax = Math.Max(legMax, deltas[i]); break;
            case "foot": footMax = Math.Max(footMax, deltas[i]); break;
            default: waistMax = Math.Max(waistMax, deltas[i]); break;
        }
    }

    Console.WriteLine(
        $"Max |dpos| by region: arm={armMax:F3} rad, hip/knee={legMax:F3} rad, " +
        $"foot={footMax:F3} rad, waist={waistMax:F3} rad");

    var moved = 0;
    foreach (var (index, delta) in deltas.Select((d, i) => (i, d)).OrderByDescending(x => x.d))
    {
        if (delta < 0.02)
        {
            continue;
        }

        moved++;
        if (moved <= 10)
        {
            Console.WriteLine($"  [{index,2}] {JointName(index),-22} moved {delta:F3} rad from baseline");
        }
    }

    Console.WriteLine(moved == 0
        ? probeOnly
            ? "No joint moved more than 0.02 rad during the probe."
            : "No joint moved more than 0.02 rad; the action was not executed by the robot."
        : $"{moved} joint(s) moved more than 0.02 rad after the actions were sent.");
}