// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Weather;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Lavaland;

/// <summary>
/// The strong storm of lavaland: a wind no staff dispels. Every few seconds it throws everyone out in the open
/// several tiles one way or another (magboots switched on hold them closer). It may only come after <see cref="EarliestStart"/> into the round, and it brings the
/// Ash Wolf to the miners' outpost.
/// </summary>
[DataDefinition]
public sealed partial class LavalandStrongStormConfig
{
    [DataField]
    public bool Enabled = true;

    /// <summary>No strong storm before this much of the round is over.</summary>
    [DataField]
    public TimeSpan EarliestStart = TimeSpan.FromMinutes(45);

    /// <summary>How often the chance is rolled.</summary>
    [DataField]
    public TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

    [DataField]
    public float BaseChance = 0.125f;

    /// <summary>Added to the chance after each roll that brought nothing.</summary>
    [DataField]
    public float ChanceIncrease = 0.05f;

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
