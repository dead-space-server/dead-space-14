using System;
using Robust.Shared.Utility;

namespace Content.Server.DeadSpace.AntagMaps;

[RegisterComponent]
public sealed partial class PreparedRoundMapComponent : Component
{
    public ResPath Path;

    public string? ShuttleId;

    public bool SleepWhenEmpty;

    public bool Warmed;

    public bool Claimed;

    public TimeSpan LoadedAt;

    public int DisposalPassNeeded;
}
