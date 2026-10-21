// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

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
}
