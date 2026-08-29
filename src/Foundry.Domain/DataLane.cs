namespace Foundry.Domain;

/// <summary>
/// The data lane follows the content and every derivative — never the operator,
/// module, or device (implementation plan §4).
/// </summary>
public enum DataLane
{
    Green = 0,
    Amber = 1,
    Restricted = 2,
}

/// <summary>Lane inheritance rules from implementation plan §4, encoded deterministically.</summary>
public static class LanePolicy
{
    /// <summary>Unknown input defaults to Amber.</summary>
    public static DataLane DefaultForUnknown => DataLane.Amber;

    /// <summary>A derivative inherits the highest lane of its inputs.</summary>
    public static DataLane Inherit(DataLane first, DataLane second)
        => (DataLane)Math.Max((int)first, (int)second);

    /// <summary>A derivative of many inputs inherits the highest lane among them; no inputs means Green (pure parameters).</summary>
    public static DataLane Inherit(IEnumerable<DataLane> lanes)
    {
        ArgumentNullException.ThrowIfNull(lanes);

        var result = DataLane.Green;
        foreach (var lane in lanes)
        {
            result = Inherit(result, lane);
        }

        return result;
    }

    /// <summary>
    /// Automated detection may escalate a lane but may never certify content as Green:
    /// a detected lane below the current one is ignored.
    /// </summary>
    public static DataLane Escalate(DataLane current, DataLane detected)
        => Inherit(current, detected);
}
