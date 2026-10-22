// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.RCD;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.DeadSpace.RCD;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RcdBuildSiteComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Progress;

    [DataField, AutoNetworkedField]
    public float StartProgress;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan StartTime;

    [DataField, AutoNetworkedField]
    public float FullDelay;

    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField, AutoNetworkedField]
    public bool Downward;

    [AutoNetworkedField]
    public NetEntity User = NetEntity.Invalid;

    [DataField, AutoNetworkedField]
    public Box2 Path = new(-0.5f, -0.5f, 0.5f, 0.5f);

    public ProtoId<RCDPrototype> ProtoId;

    public Direction Direction;

    public bool UseDirection;

    public Vector2i Tile;

    public NetEntity Target = NetEntity.Invalid;

    public NetEntity Grid;

    public TimeSpan Touched;
}
