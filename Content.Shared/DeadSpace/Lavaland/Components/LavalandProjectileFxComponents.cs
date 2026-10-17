// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Lavaland.Components;

/// <summary>A fading glowing trail behind the entity (Goob-Station Trail), drawn by the client.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LavalandProjectileTrailComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color Color = Color.White;

    /// <summary>Radius of a fresh trail puff, in tiles.</summary>
    [DataField, AutoNetworkedField]
    public float Size = 0.3f;

    /// <summary>Seconds a puff lives.</summary>
    [DataField, AutoNetworkedField]
    public float Lifetime = 0.6f;

    /// <summary>Seconds between puffs.</summary>
    [DataField, AutoNetworkedField]
    public float Interval = 0.02f;
}

/// <summary>The projectile turns towards the nearest living creature ahead of it.</summary>
[RegisterComponent]
public sealed partial class LavalandHomingProjectileComponent : Component
{
    /// <summary>How far it looks for a target, in tiles.</summary>
    [DataField]
    public float Range = 8f;

    /// <summary>Only targets within this angle of the flight direction, degrees.</summary>
    [DataField]
    public float ConeAngle = 70f;

    /// <summary>Turn speed, degrees per second.</summary>
    [DataField]
    public float TurnRate = 540f;

    [ViewVariables]
    public EntityUid? Target;
}
