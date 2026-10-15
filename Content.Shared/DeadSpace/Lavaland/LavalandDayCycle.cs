// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Lavaland;

/// <summary>
/// Day/night cycle of a lavaland planet. Phases run in order and loop; the ambient light fades
/// between their colors over <see cref="TransitionTime"/>.
/// </summary>
[DataDefinition]
public sealed partial class LavalandDayCycleConfig
{
    [DataField]
    public bool Enabled = true;

    /// <summary>How long the light takes to fade into the next phase color.</summary>
    [DataField]
    public TimeSpan TransitionTime = TimeSpan.FromSeconds(60);

    /// <summary>How often the faded light color is sent to clients during a transition.</summary>
    [DataField]
    public TimeSpan LightUpdateInterval = TimeSpan.FromSeconds(1);

    /// <summary>Index of the phase the planet starts in.</summary>
    [DataField]
    public int StartPhase;

    [DataField]
    public List<LavalandDayPhase> Phases = new();
}

[DataDefinition]
public sealed partial class LavalandDayPhase
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromMinutes(15);

    /// <summary>Ambient light of the map in this phase. Black means nothing is visible without a light source.</summary>
    [DataField]
    public Color LightColor = Color.FromHex("#A34931");

    /// <summary>Announcement shown to everyone on the map when the phase begins. Null for none.</summary>
    [DataField]
    public LocId? Message;

    [DataField]
    public Color MessageColor = Color.OrangeRed;

    [DataField]
    public SoundSpecifier? Sound;

    /// <summary>GPS shows garbage and the sensor tablet loses and scatters its marks.</summary>
    [DataField]
    public bool JamTrackers;

    /// <summary>Jaunters do nothing, including the automatic chasm save.</summary>
    [DataField]
    public bool BlockJaunter;

    /// <summary>Released lantern wisps leave their owner and lead them to the outpost.</summary>
    [DataField]
    public bool WispGuide;

    /// <summary>With some chance this phase turns into a deep night for a while. Null for never.</summary>
    [DataField]
    public LavalandDeepNightConfig? DeepNight;
}

/// <summary>
/// Deep night: a warning, then the moon hides for <see cref="Duration"/> - the light dies, equipment fails and the
/// darkness burns; then the usual night goes on for <see cref="AfterDuration"/>.
/// </summary>
[DataDefinition]
public sealed partial class LavalandDeepNightConfig
{
    [DataField]
    public float Chance = 0.3f;

    /// <summary>Between the warning and the moon hiding; the light fades to black meanwhile.</summary>
    [DataField]
    public TimeSpan WarningTime = TimeSpan.FromSeconds(60);

    [DataField]
    public TimeSpan Duration = TimeSpan.FromMinutes(5);

    /// <summary>Usual night after the deep one.</summary>
    [DataField]
    public TimeSpan AfterDuration = TimeSpan.FromMinutes(10);

    [DataField]
    public Color LightColor = Color.Black;

    [DataField]
    public LocId? Warning;

    [DataField]
    public LocId? Message;

    [DataField]
    public LocId? EndMessage;

    [DataField]
    public Color MessageColor = Color.FromHex("#8a1c1c");

    [DataField]
    public SoundSpecifier? WarningSound;

    [DataField]
    public SoundSpecifier? Sound;

    [DataField]
    public bool JamTrackers = true;

    [DataField]
    public bool BlockJaunter = true;

    [DataField]
    public bool WispGuide = true;

    /// <summary>Night vision just does not turn on.</summary>
    [DataField]
    public bool BlockNightVision = true;

    /// <summary>The lava stops glowing.</summary>
    [DataField]
    public bool SuppressLavaLight = true;

    /// <summary>PDA light: dies after this long, then cannot be turned on for <see cref="PdaLightCooldown"/>.</summary>
    [DataField]
    public TimeSpan PdaLightTime = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan PdaLightCooldown = TimeSpan.FromMinutes(3);

    /// <summary>Helmet lights (hardsuits and the like).</summary>
    [DataField]
    public TimeSpan HelmetLightTime = TimeSpan.FromSeconds(25);

    [DataField]
    public TimeSpan HelmetLightCooldown = TimeSpan.FromMinutes(3);

    /// <summary>Other flashlights (the seclite included): after this long the battery dies.</summary>
    [DataField]
    public TimeSpan FlashlightTime = TimeSpan.FromSeconds(60);

    /// <summary>Released lantern wisps shine this much in the deep night.</summary>
    [DataField]
    public float WispLightRadius = 10f;

    [DataField]
    public float WispLightEnergy = 3f;

    /// <summary>How long a player may stay out of any light before the darkness starts to burn.</summary>
    [DataField]
    public TimeSpan DarknessGrace = TimeSpan.FromSeconds(15);

    [DataField]
    public TimeSpan DarknessInterval = TimeSpan.FromSeconds(15);

    /// <summary>Dealt each interval in the dark, only while the player is not yet critical.</summary>
    [DataField]
    public DamageSpecifier DarknessDamage = new()
    {
        DamageDict = new() { { "Heat", 25 } },
    };

    /// <summary>Lights farther than this are not looked at.</summary>
    [DataField]
    public float LightSearchRange = 12f;
}

public enum LavalandDeepNightStage : byte
{
    None,
    Warning,
    Active,
}

/// <summary>Current day phase of a lavaland map. Lives on the map entity.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LavalandDayCycleComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Phase;

    [DataField, AutoNetworkedField]
    public bool JamTrackers;

    [DataField, AutoNetworkedField]
    public bool BlockJaunter;

    [DataField, AutoNetworkedField]
    public bool WispGuide;

    /// <summary>The moon is hidden: see <see cref="LavalandDeepNightConfig"/>.</summary>
    [DataField, AutoNetworkedField]
    public bool DeepNight;

    [DataField, AutoNetworkedField]
    public bool SuppressLavaLight;

    [ViewVariables]
    public LavalandDeepNightStage DeepStage;

    [ViewVariables]
    public TimeSpan DeepStartAt;

    [ViewVariables]
    public TimeSpan DeepEndAt;

    /// <summary>When the current light fade began.</summary>
    [ViewVariables]
    public TimeSpan FadeStart;

    [ViewVariables]
    public TimeSpan PhaseStart;

    [ViewVariables]
    public TimeSpan PhaseEnd;

    [ViewVariables]
    public Color FromColor;

    [ViewVariables]
    public Color ToColor;

    [ViewVariables]
    public TimeSpan NextLightUpdate;

    [ViewVariables]
    public bool Fading;
}

public sealed class LavalandDayCycleSystem : EntitySystem
{
    /// <summary>True if trackers on this map are jammed by the current phase.</summary>
    public bool IsTrackerJammed(EntityUid? map)
    {
        return TryComp<LavalandDayCycleComponent>(map, out var cycle) && cycle.JamTrackers;
    }

    public bool IsJaunterBlocked(EntityUid? map)
    {
        return TryComp<LavalandDayCycleComponent>(map, out var cycle) && cycle.BlockJaunter;
    }

    public bool IsWispGuiding(EntityUid? map)
    {
        return TryComp<LavalandDayCycleComponent>(map, out var cycle) && cycle.WispGuide;
    }

    public bool IsDeepNight(EntityUid? map)
    {
        return TryComp<LavalandDayCycleComponent>(map, out var cycle) && cycle.DeepNight;
    }

    /// <summary>Lava on this map does not glow.</summary>
    public bool IsLavaLightSuppressed(EntityUid? map)
    {
        return TryComp<LavalandDayCycleComponent>(map, out var cycle) && cycle.DeepNight && cycle.SuppressLavaLight;
    }
}
