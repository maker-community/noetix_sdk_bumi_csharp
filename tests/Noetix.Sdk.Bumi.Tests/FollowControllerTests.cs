using Noetix.Sdk.Bumi.FollowDemo;
using Xunit;

namespace Noetix.Sdk.Bumi.Tests;

public class FollowControllerTests
{
    [Fact]
    public void SyntheticObservationUsesActualWorldPositionsAndHeading()
    {
        var observation = SimulatedPersonTarget.Observe(10, -3, Math.PI / 2, 10, 0, true);
        Assert.Equal(3, observation.DistanceMeters, 6);
        Assert.Equal(0, observation.BearingRadians, 6);
    }

    [Fact]
    public void TargetLossStopsImmediatelyAndNeverStartsSearching()
    {
        var controller = new FollowController();
        controller.Engage();
        Assert.True(controller.Update(new FollowObservation(4, 0, true), 0.1).Forward > 0);
        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(default, controller.Update(new FollowObservation(4, 1, false), 0.1));
        }
        Assert.Equal(FollowState.TargetLost, controller.State);
    }

    [Fact]
    public void TooCloseStopsBothAxesImmediatelyEvenWhenAlreadyMoving()
    {
        var controller = new FollowController();
        controller.Engage();
        var moving = controller.Update(new FollowObservation(4, 0.5, true), 0.1);
        Assert.True(moving.Forward > 0);
        Assert.True(moving.Yaw > 0);
        Assert.Equal(default, controller.Update(new FollowObservation(1, 0.5, true), 0.1));
    }

    [Fact]
    public void HysteresisKeepsStoppedUntilTargetMovesBeyondRestartDistance()
    {
        var controller = new FollowController();
        controller.Engage();
        Assert.Equal(default, controller.Update(new FollowObservation(2, 0, true), 0.1));
        Assert.Equal(default, controller.Update(new FollowObservation(2.5, 0, true), 0.1));
        Assert.True(controller.Update(new FollowObservation(2.7, 0, true), 0.1).Forward > 0);
    }

    [Theory]
    [InlineData(double.NaN, 0, 0.1)]
    [InlineData(-1, 0, 0.1)]
    [InlineData(3, double.PositiveInfinity, 0.1)]
    [InlineData(3, 0, 0)]
    [InlineData(3, 0, 0.3)]
    public void InvalidInputStops(double distance, double bearing, double dt)
    {
        var controller = new FollowController();
        controller.Engage();
        Assert.Equal(default, controller.Update(new FollowObservation(distance, bearing, true), dt));
    }

    [Fact]
    public void InvalidSettingsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new FollowController(new FollowSettings { MaxForward = double.NaN }));
        Assert.Throws<ArgumentException>(() => new FollowController(new FollowSettings { RestartDistance = 1 }));
    }
}
