using Foundry.Domain;
using Xunit;

namespace Foundry.Tests.Unit;

public class LanePolicyTests
{
    [Fact]
    public void Unknown_input_defaults_to_amber()
    {
        Assert.Equal(DataLane.Amber, LanePolicy.DefaultForUnknown);
    }

    [Theory]
    [InlineData(DataLane.Green, DataLane.Green, DataLane.Green)]
    [InlineData(DataLane.Green, DataLane.Amber, DataLane.Amber)]
    [InlineData(DataLane.Amber, DataLane.Green, DataLane.Amber)]
    [InlineData(DataLane.Amber, DataLane.Restricted, DataLane.Restricted)]
    [InlineData(DataLane.Restricted, DataLane.Green, DataLane.Restricted)]
    public void Derivative_inherits_the_highest_lane(DataLane first, DataLane second, DataLane expected)
    {
        Assert.Equal(expected, LanePolicy.Inherit(first, second));
    }

    [Fact]
    public void Many_inputs_inherit_the_highest_among_them()
    {
        Assert.Equal(
            DataLane.Restricted,
            LanePolicy.Inherit([DataLane.Green, DataLane.Restricted, DataLane.Amber]));
    }

    [Fact]
    public void No_inputs_means_green_pure_parameters()
    {
        Assert.Equal(DataLane.Green, LanePolicy.Inherit([]));
    }

    [Theory]
    [InlineData(DataLane.Amber, DataLane.Green, DataLane.Amber)]
    [InlineData(DataLane.Restricted, DataLane.Green, DataLane.Restricted)]
    [InlineData(DataLane.Green, DataLane.Amber, DataLane.Amber)]
    public void Detection_escalates_but_never_certifies_green(DataLane current, DataLane detected, DataLane expected)
    {
        Assert.Equal(expected, LanePolicy.Escalate(current, detected));
    }
}
