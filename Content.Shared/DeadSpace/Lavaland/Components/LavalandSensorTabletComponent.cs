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
    [DataField] public float MobPointSize = 0.65f;
    [DataField] public float TerrainPointSize = 0.52f;
    [DataField] public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);
    public TimeSpan NextUpdate;
}
