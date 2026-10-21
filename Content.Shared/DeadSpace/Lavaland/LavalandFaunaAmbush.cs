// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Lavaland;

/// <summary>A goldgrub may dig out of the rock a miner is breaking, jump at them and eat their ore.</summary>
[DataDefinition]
public sealed partial class LavalandGoldgrubAmbushConfig
{
    [DataField]
    public bool Enabled = true;

    /// <summary>Chance per rock a player breaks.</summary>
    [DataField]
    public float Chance = 0.015f;

    /// <summary>The same player is not ambushed again for this long.</summary>
    [DataField]
    public TimeSpan PlayerCooldown = TimeSpan.FromMinutes(5);

    /// <summary>Only players carrying ore the goldgrub likes (see its AmbushOre) get ambushed.</summary>
    [DataField]
    public bool RequireOre = true;

    [DataField]
    public EntProtoId Prototype = "MobLavalandGoldgrub";
}

/// <summary>
/// Wild ash whelps: they cannot be killed - they drop from the sky, spit a line of fire and fly away.
/// They come now and then to a random player, and from nests scattered over the map when someone walks by.
/// </summary>
[DataDefinition]
public sealed partial class LavalandWildWhelpConfig
{
    [DataField]
    public bool Enabled = true;

    [DataField]
    public EntProtoId Prototype = "MobLavalandWildAshWhelp";

    /// <summary>How often a random player may be visited.</summary>
    [DataField]
    public TimeSpan RandomInterval = TimeSpan.FromSeconds(90);

    [DataField]
    public float RandomChance = 0.25f;

    /// <summary>After a visit (random or from a nest) the player is left alone for this long.</summary>
    [DataField]
    public TimeSpan PlayerCooldown = TimeSpan.FromMinutes(4);

    [DataField]
    public int MinWhelps = 1;

    [DataField]
    public int MaxWhelps = 2;

    [DataField]
    public float SpawnDistanceMin = 3f;

    [DataField]
    public float SpawnDistanceMax = 5f;
}

/// <summary>Drops the sprite from the sky (or lifts it up when <see cref="Rising"/>), drawn by the client.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LavalandSkyDropComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Start;

    [DataField, AutoNetworkedField]
    public float Duration = 0.8f;

    /// <summary>Height in tiles.</summary>
    [DataField, AutoNetworkedField]
    public float Height = 8f;

    [DataField, AutoNetworkedField]
    public bool Rising;

    /// <summary>Sinking into a pit instead: shrinking away where it lies, as things falling into a chasm do.</summary>
    [DataField, AutoNetworkedField]
    public bool Sinking;

    /// <summary>Sideways offset per tile of height: it comes down (or goes up) at a slant, not straight.</summary>
    [DataField, AutoNetworkedField]
    public float Slant;
}
