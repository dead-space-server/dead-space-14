// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Lavaland.Bosses;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Parallax.Biomes.Markers;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Lavaland.ChromiteCaves;

/// <summary>
/// Chromite caves under lavaland. A shimmering pit near the outpost leads down; there is no way back up without
/// a grappling hook (anything with <c>GrapplingGun</c>). The caves' light follows the lavaland day: lavaland day -
/// a deep night down here, lavaland night - an ordinary night, lavaland deep night - the caves glow.
/// Everything here is set in the lavaland planet prototype (chromiteCaves).
/// </summary>
[DataDefinition]
public sealed partial class LavalandChromiteCavesConfig
{
    [DataField]
    public bool Enabled = true;

    #region Map

    [DataField]
    public ProtoId<BiomeTemplatePrototype> Biome = "LavalandChromiteCaves";

    [DataField]
    public List<ProtoId<BiomeMarkerLayerPrototype>> MarkerLayers = new();

    [DataField]
    public string MapName = "Chromite Caves";

    [DataField]
    public int MapHalfSize = 140;

    [DataField]
    public int BoundaryWidth = 3;

    [DataField]
    public string BoundaryTile = "FloorChromite";

    [DataField]
    public EntProtoId BoundaryWall = "WallRockChromiteCavesBoundary";

    #endregion

    #region Entrance

    /// <summary>One tile of the pit; the pit is a square of these.</summary>
    [DataField]
    public EntProtoId PitPrototype = "LavalandChromitePit";

    /// <summary>The pit is (2 * radius + 1) tiles across.</summary>
    [DataField]
    public int PitRadius = 1;

    /// <summary>Standing this close to the edge of the pit asks whether to jump.</summary>
    [DataField]
    public float JumpPromptRange = 1.2f;

    /// <summary>After declining, the question comes back after this long.</summary>
    [DataField]
    public TimeSpan JumpPromptCooldown = TimeSpan.FromSeconds(15);

    /// <summary>Distance from the outpost center to the pit.</summary>
    [DataField]
    public float PitMinDistance = 42f;

    [DataField]
    public float PitMaxDistance = 58f;

    #endregion

    #region Landing and the exit

    /// <summary>Rock is cleared this far around the landing point.</summary>
    [DataField]
    public int LandingClearRadius = 7;

    /// <summary>The caves are generated this far around the landing point with the planet, so landing doesn't stall.</summary>
    [DataField]
    public float PreloadRadius = 40f;

    /// <summary>Glowing flowers cover the landing point this far.</summary>
    [DataField]
    public int LandingFlowerRadius = 5;

    [DataField]
    public float LandingFlowerChance = 0.8f;

    [DataField]
    public string LandingTile = "FloorChromite";

    [DataField]
    public List<EntProtoId> LandingFlowers = new() { "LavalandChromiteLandingFlower" };

    /// <summary>The beam of light from above at the landing point: the only way up.</summary>
    [DataField]
    public EntProtoId ExitPrototype = "LavalandChromiteExit";

    /// <summary>Petals bursting up where somebody lands.</summary>
    [DataField]
    public EntProtoId? LandingEffect = "LavalandChromiteLandingPetals";

    /// <summary>Standing this close to the beam asks whether to climb out.</summary>
    [DataField]
    public float ExitRange = 1.3f;

    [DataField]
    public TimeSpan ExitPromptCooldown = TimeSpan.FromSeconds(15);

    #endregion

    #region Timings

    [DataField]
    public TimeSpan JumpDuration = TimeSpan.FromSeconds(1.1);

    [DataField]
    public TimeSpan FallDuration = TimeSpan.FromSeconds(4.5);

    [DataField]
    public TimeSpan LandingDuration = TimeSpan.FromSeconds(2.4);

    /// <summary>Jumping on the spot without a hook, before the mocking message.</summary>
    [DataField]
    public TimeSpan HopDuration = TimeSpan.FromSeconds(2.1);

    /// <summary>The hook flies up with its rope.</summary>
    [DataField]
    public TimeSpan RopeDuration = TimeSpan.FromSeconds(0.9);

    /// <summary>The player is pulled up out of sight.</summary>
    [DataField]
    public TimeSpan FlyUpDuration = TimeSpan.FromSeconds(0.9);

    /// <summary>Hanging on the rope on the way up.</summary>
    [DataField]
    public TimeSpan HangDuration = TimeSpan.FromSeconds(4);

    /// <summary>Climbing out over the edge of the pit on lavaland.</summary>
    [DataField]
    public TimeSpan ClimbOutDuration = TimeSpan.FromSeconds(1.4);

    [DataField]
    public SoundSpecifier? JumpSound = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg");

    [DataField]
    public SoundSpecifier? LandSound = new SoundPathSpecifier("/Audio/Effects/bodyfall1.ogg");

    [DataField]
    public SoundSpecifier? RopeSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Gunshots/harpoon.ogg");

    #endregion

    #region Bosses

    /// <summary>Boss arenas built in the caves (the Hierophant lives here).</summary>
    [DataField]
    public Dictionary<ProtoId<LavalandBossArenaPrototype>, int> BossArenas = new();

    /// <summary>An elite waiting somewhere in the caves, like the mad miner on the surface.</summary>
    [DataField]
    public EntProtoId? Elite = "MobLavalandPandora";

    [DataField]
    public float EliteMinDistance = 45f;

    [DataField]
    public float EliteMaxDistance = 105f;

    /// <summary>A clearing around the elite so it has room to fight.</summary>
    [DataField]
    public int EliteClearRadius = 5;

    #endregion

    #region Light

    /// <summary>While it is day on lavaland it is deep night in the caves.</summary>
    [DataField]
    public Color DeepDarkLight = Color.FromHex("#040510");

    /// <summary>While it is night on lavaland it is an ordinary night in the caves, most things are visible.</summary>
    [DataField]
    public Color NightLight = Color.FromHex("#2B3A70");

    /// <summary>While it is deep night on lavaland the caves glow like the milky way.</summary>
    [DataField]
    public Color GlowLight = Color.FromHex("#2A2F78");

    [DataField]
    public TimeSpan LightFade = TimeSpan.FromSeconds(30);

    /// <summary>The deep night of the caves (during the lavaland day): equipment fails, the darkness burns.</summary>
    [DataField]
    public LavalandDeepNightConfig DeepNight = new();

    [DataField]
    public LocId? DeepDarkMessage = "lavaland-caves-deep-dark";

    [DataField]
    public LocId? NightMessage = "lavaland-caves-night";

    [DataField]
    public LocId? GlowMessage = "lavaland-caves-glow";

    [DataField]
    public Color MessageColor = Color.FromHex("#7fc8ff");

    #endregion
}
