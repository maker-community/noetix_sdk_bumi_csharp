namespace Noetix.Sdk.Bumi;

/// <summary>Mirrors <c>noetix::ControlCmd</c>. Numeric values are passed to the SDK and must not change.</summary>
public enum ControlCmd : uint
{
    Walk = 0,
    Swing = 1,
    Shake = 2,
    Cheer = 3,
    /// <summary>Reserved by the official SDK; currently unavailable.</summary>
    Run = 4,
    Start = 5,
    Switch = 6,
    StartTeach = 7,
    SaveTeach = 8,
    EndTeach = 9,
    PlayTeach = 10,
    Dance = 11,
    FallToStand = 12,
    StandToFall = 13,
    Dance1 = 14,
    Dance2 = 15,
    Tear = 16,
    Default = 17,
}
