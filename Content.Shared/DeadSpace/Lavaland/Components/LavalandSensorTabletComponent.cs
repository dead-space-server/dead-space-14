// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;
using Content.Shared.DeadSpace.Lavaland;

namespace Content.Shared.DeadSpace.Lavaland.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class LavalandSensorTabletComponent : Component
{
    [DataField] public float ScanRadius = 10f;
    [DataField] public Color LivingColor = Color.White;
    [DataField] public Color DeadColor = Color.Black;
    [DataField] public Color FaunaColor = Color.Red;
    [DataField] public Color OreColor = Color.Gold;
    [DataField] public Color LavaColor = Color.OrangeRed;
    [DataField] public Color FloorColor = new(80, 55, 45);
    [DataField] public Color RockColor = new(150, 105, 75);
    [DataField] public Color BaseFloorColor = new(40, 75, 90);
    [DataField] public Color BaseWallColor = new(115, 210, 230);
    [DataField] public Color MorkiteHintColor = new(40, 220, 190, 180);
    [DataField] public float MorkiteHintRadius = 20f;
    [DataField] public float MobPointSize = 0.65f;
    [DataField] public float TerrainPointSize = 0.52f;
    [DataField] public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    /// <summary>While a day phase jams trackers: chance for a miner mark to vanish for an update.</summary>
    [DataField] public float JamDropChance = 0.5f;

    /// <summary>While jammed, miner marks are shown up to this many tiles away from the real spot.</summary>
    [DataField] public float JamScatter = 15f;
    public TimeSpan NextUpdate;
    public TimeSpan NextBeep;

    /// <summary>Version of the outpost plan last sent to the UI of this tablet; -1 sends it again.</summary>
    public int SentBaseVersion = -1;

    /// <summary>
    /// The lead salvager's tablet: shows the layer chosen on it (surface or caves) wherever it is. An ordinary one
    /// shows only the layer it is on.
    /// </summary>
    [DataField] public bool CanSwitchLayer;

    /// <summary>The layer chosen on a tablet that can switch: the chromite caves instead of the surface.</summary>
    [ViewVariables] public bool ViewCaves;

    /// <summary>The item slot the sonar cartridge goes into.</summary>
    [DataField] public string SonarSlot = "sonar_cartridge";

    /// <summary>Between two sonar pings.</summary>
    [DataField] public TimeSpan SonarCooldown = TimeSpan.FromSeconds(3);

    /// <summary>A sonar ping farther than this (tiles) is the most blurred.</summary>
    [DataField] public float SonarFuzzRange = 250f;

    /// <summary>The sonar catches mini-bosses only this close (tiles).</summary>
    [DataField] public float SonarMinibossRange = 60f;

    public TimeSpan NextSonar;
    public LavalandSonarPing? LastPing;
    public int PingCounter;
}

/// <summary>A cartridge for the lavaland tablet's sonar: each ping spends one charge.</summary>
[RegisterComponent]
public sealed partial class LavalandSonarCartridgeComponent : Component
{
    [DataField] public int Charges = 10;
    [DataField] public int MaxCharges = 10;
}
