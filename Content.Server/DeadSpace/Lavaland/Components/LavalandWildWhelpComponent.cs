// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Lavaland.Components;

public enum LavalandWildWhelpStage : byte
{
    Dropping,
    Aiming,
    Fleeing,
    Rising,
}

/// <summary>
/// A wild ash whelp that cannot be killed: drops from the sky next to a player, telegraphs and spits a line of fire,
/// then flees and flies away.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandWildWhelpComponent : Component
{
    [DataField]
    public float DropTime = 0.8f;

    [DataField]
    public float DropHeight = 8f;

    /// <summary>Between landing and the fire: the tiles are marked meanwhile.</summary>
    [DataField]
    public TimeSpan AimTime = TimeSpan.FromSeconds(1.1);

    /// <summary>Length of the fire line, in tiles; it is three tiles wide.</summary>
    [DataField]
    public int FireRange = 5;

    [DataField]
    public EntProtoId FirePrototype = "LavalandAshDrakeFire";

    [DataField]
    public EntProtoId TelegraphPrototype = "LavalandAshDrakeWhelpTarget";

    [DataField]
    public EntProtoId LandingMarkerPrototype = "LavalandAshDrakeTarget";

    [DataField]
    public SoundSpecifier? LandSound = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg");

    [DataField]
    public SoundSpecifier? FireSound = new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/AshDrake/fireball.ogg");

    [DataField]
    public float FleeSpeed = 6f;

    [DataField]
    public TimeSpan FleeTime = TimeSpan.FromSeconds(1.6);

    [DataField]
    public float RiseTime = 0.7f;

    [ViewVariables]
    public LavalandWildWhelpStage Stage;

    [ViewVariables]
    public TimeSpan StageEnd;

    [ViewVariables]
    public EntityUid? Target;

    [ViewVariables]
    public Vector2i Direction;

    [ViewVariables]
    public readonly List<Vector2i> Tiles = new();

    [ViewVariables]
    public readonly List<EntityUid> Telegraphs = new();
}

/// <summary>Admin test: on spawn, wild whelps drop on the nearest player right away (no chance, no cooldown).</summary>
[RegisterComponent]
public sealed partial class LavalandWildWhelpTestComponent : Component
{
    [DataField]
    public float Range = 15f;
}

/// <summary>A hidden nest: when a player walks close, wild whelps come down on them.</summary>
[RegisterComponent]
public sealed partial class LavalandWildWhelpNestComponent : Component
{
    [DataField]
    public float Radius = 4f;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(5);

    [ViewVariables]
    public TimeSpan NextTrigger;
}
