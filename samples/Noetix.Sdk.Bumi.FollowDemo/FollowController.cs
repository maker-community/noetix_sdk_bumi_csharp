namespace Noetix.Sdk.Bumi.FollowDemo;

public enum FollowState
{
    Disengaged,
    Following,
    TargetLost,
}

public readonly record struct FollowObservation(double DistanceMeters, double BearingRadians, bool TargetVisible);

public readonly record struct FollowCommand(double Forward, double Yaw);

/// <summary>Simulation-only thresholds, not a safety specification for a robot.</summary>
public sealed class FollowSettings
{
    public double StopDistance { get; init; } = 2.0;
    public double RestartDistance { get; init; } = 2.6;
    public double MaxForward { get; init; } = 0.3;
    public double MaxYaw { get; init; } = 0.2;
    public double ForwardGain { get; init; } = 0.2;
    public double YawGain { get; init; } = 0.5;
    public double BearingDeadband { get; init; } = 0.05;
    public double SlewRatePerSecond { get; init; } = 0.5;
}

/// <summary>Experimental follower for synthetic, simulation-ground-truth observations only.</summary>
public sealed class FollowController
{
    private readonly FollowSettings _settings;
    private bool _waitingToRestart = true;
    private FollowCommand _lastCommand;

    public FollowController(FollowSettings? settings = null)
    {
        _settings = settings ?? new FollowSettings();
        if (!double.IsFinite(_settings.StopDistance) || _settings.StopDistance <= 0 ||
            !double.IsFinite(_settings.RestartDistance) || _settings.RestartDistance <= _settings.StopDistance ||
            !double.IsFinite(_settings.MaxForward) || _settings.MaxForward is <= 0 or > 1 ||
            !double.IsFinite(_settings.MaxYaw) || _settings.MaxYaw is <= 0 or > 1 ||
            !double.IsFinite(_settings.ForwardGain) || _settings.ForwardGain <= 0 ||
            !double.IsFinite(_settings.YawGain) || _settings.YawGain <= 0 ||
            !double.IsFinite(_settings.BearingDeadband) || _settings.BearingDeadband < 0 ||
            !double.IsFinite(_settings.SlewRatePerSecond) || _settings.SlewRatePerSecond <= 0)
        {
            throw new ArgumentException("Invalid follow settings.", nameof(settings));
        }
    }

    public FollowState State { get; private set; } = FollowState.Disengaged;

    public void Engage() => State = FollowState.TargetLost;

    public void Disengage()
    {
        State = FollowState.Disengaged;
        Stop();
    }

    public FollowCommand Update(in FollowObservation observation, double dtSeconds)
    {
        if (State == FollowState.Disengaged)
        {
            return Stop();
        }

        if (!observation.TargetVisible || !double.IsFinite(observation.DistanceMeters) ||
            observation.DistanceMeters <= 0 || !double.IsFinite(observation.BearingRadians) ||
            !double.IsFinite(dtSeconds) || dtSeconds <= 0 || dtSeconds > 0.2)
        {
            State = FollowState.TargetLost;
            return Stop();
        }

        State = FollowState.Following;
        if (observation.DistanceMeters <= _settings.StopDistance)
        {
            return Stop();
        }

        if (_waitingToRestart && observation.DistanceMeters < _settings.RestartDistance)
        {
            return Stop();
        }

        _waitingToRestart = false;
        var forward = Math.Clamp(
            (observation.DistanceMeters - _settings.StopDistance) * _settings.ForwardGain,
            0, _settings.MaxForward);
        var yaw = Math.Abs(observation.BearingRadians) <= _settings.BearingDeadband
            ? 0
            : Math.Clamp(observation.BearingRadians * _settings.YawGain, -_settings.MaxYaw, _settings.MaxYaw);
        var delta = _settings.SlewRatePerSecond * dtSeconds;
        _lastCommand = new FollowCommand(Slew(_lastCommand.Forward, forward, delta), Slew(_lastCommand.Yaw, yaw, delta));
        return _lastCommand;
    }

    private FollowCommand Stop()
    {
        _waitingToRestart = true;
        _lastCommand = default;
        return _lastCommand;
    }

    private static double Slew(double current, double target, double delta) =>
        Math.Clamp(target, current - delta, current + delta);
}
