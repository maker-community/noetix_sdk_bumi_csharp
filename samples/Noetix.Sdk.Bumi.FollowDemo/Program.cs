using System.Globalization;
using Noetix.Sdk.Bumi.FollowDemo;

// This process has no SDK or network connection: its input and output are simulator pipes.
if (args.Length != 1 || args[0] != "--offline-simulation")
{
    Console.Error.WriteLine("Only --offline-simulation is supported; this program cannot control a robot.");
    return 1;
}

var controller = new FollowController();
controller.Engage();
string? line;
while ((line = Console.ReadLine()) is not null)
{
    var fields = line.Split(',');
    if (fields.Length != 7 ||
        !double.TryParse(fields[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var robotX) ||
        !double.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var robotY) ||
        !double.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var yaw) ||
        !double.TryParse(fields[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var targetX) ||
        !double.TryParse(fields[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var targetY) ||
        !bool.TryParse(fields[5], out var visible) ||
        !double.TryParse(fields[6], NumberStyles.Float, CultureInfo.InvariantCulture, out var dt))
    {
        Console.Error.WriteLine("Invalid simulator observation.");
        return 2;
    }

    var observation = SimulatedPersonTarget.Observe(robotX, robotY, yaw, targetX, targetY, visible);
    var command = controller.Update(observation, dt);
    Console.WriteLine(FormattableString.Invariant(
        $"{command.Forward:R},{command.Yaw:R},{observation.DistanceMeters:R},{controller.State}"));
}

return 0;
