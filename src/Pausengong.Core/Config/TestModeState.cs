namespace Pausengong.Core.Config;

/// <summary>
/// Safety-critical gate: decides whether a ring command plays a real gong or shows a
/// toast notification instead. This is the single chokepoint for that decision — every
/// other component must go through it rather than deciding independently.
///
/// Defaults to active (test mode ON) on any fresh config, so a node that has never
/// synced config with the rest of the network cannot accidentally ring a real gong.
/// </summary>
public sealed record TestModeState(bool IsActive, DateTimeOffset UpdatedAt)
{
    public static TestModeState Default { get; } = new(IsActive: true, DateTimeOffset.MinValue);
}
