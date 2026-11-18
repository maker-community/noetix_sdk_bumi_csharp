namespace Noetix.Sdk.Bumi.FollowDemo;

/// <summary>Computes a synthetic observation from positions in the same simulated world.</summary>
public static class SimulatedPersonTarget
{
    public static FollowObservation Observe(
        double robotX, double robotY, double robotYaw, double personX, double personY, bool visible)
    {
        var dx = personX - robotX;
        var dy = personY - robotY;
        var bearing = Math.Atan2(dy, dx) - robotYaw;
        return new FollowObservation(Math.Sqrt((dx * dx) + (dy * dy)),
            Math.Atan2(Math.Sin(bearing), Math.Cos(bearing)), visible);
    }
}
