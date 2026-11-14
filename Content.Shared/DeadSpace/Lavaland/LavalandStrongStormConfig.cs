// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage;
using Content.Shared.Weather;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Lavaland;

/// <summary>
/// The strong storm of lavaland: a wind no staff dispels. Every few seconds it throws everyone out in the open
/// several tiles one way or another (magboots switched on hold them closer) and burns them like the ash storm. It comes
/// in place of an ordinary ash storm, only after <see cref="EarliestStart"/> into the round, and it brings the Ash Wolf
/// to the miners' outpost.
/// </summary>
[DataDefinition]
public sealed partial class LavalandStrongStormConfig
{
    [DataField]
    public bool Enabled = true;

    /// <summary>No strong storm before this much of the round is over.</summary>
    [DataField]
    public TimeSpan EarliestStart = TimeSpan.FromMinutes(45);

    /// <summary>The chance that an ordinary ash storm, about to come, is the strong one instead.</summary>
    [DataField]
    public float Chance = 0.15f;

    /// <summary>What it does to everyone out in the open, every <see cref="DamageInterval"/>.</summary>
    [DataField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = new()
        {
            { "Heat", 4.0f },
        },
    };

    [DataField]
    public TimeSpan DamageInterval = TimeSpan.FromSeconds(1);

    /// <summary>The alarm before the wind rises.</summary>
    [DataField]
    public TimeSpan WarningDuration = TimeSpan.FromSeconds(60);

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(180);

    [DataField]
    public TimeSpan ThrowInterval = TimeSpan.FromSeconds(3);

    /// <summary>How far a gust throws someone (tiles).</summary>
    [DataField]
    public float ThrowDistance = 7f;

    /// <summary>How far a gust throws someone with magboots switched on (tiles).</summary>
    [DataField]
    public float MagbootsThrowDistance = 3f;

    [DataField]
    public float ThrowSpeed = 9f;

    [DataField]
    public ProtoId<WeatherPrototype>? WarningWeather = "AshfallLight";

    [DataField]
    public ProtoId<WeatherPrototype>? Weather = "AshfallHeavy";

    [DataField]
    public LocId WarningMessage = "lavaland-strong-storm-warning";

    [DataField]
    public LocId StartMessage = "lavaland-strong-storm-start";

    [DataField]
    public LocId EndMessage = "lavaland-strong-storm-end";

    [DataField]
    public Color MessageColor = Color.FromHex("#8a1c1c");

    [DataField]
    public SoundSpecifier? WarningSound = new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/lava_warning.ogg");

    /// <summary>The secret boss the storm brings to the outpost (lying there until somebody strikes it).</summary>
    [DataField]
    public EntProtoId? Boss = "MobLavalandAshWolf";

    /// <summary>How far from the outpost's edge it lies (tiles).</summary>
    [DataField]
    public float BossDistance = 12f;
}
