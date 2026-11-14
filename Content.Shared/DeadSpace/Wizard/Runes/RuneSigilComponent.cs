// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.DeadSpace.Wizard.Runes;

[Serializable, NetSerializable]
public enum RuneSigilState : byte
{
    Drawing,
    Cast,
    Fizzle,
    Fade,
}

[Serializable, NetSerializable]
public sealed class RuneSigilClearMessage : EntityEventArgs
{
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RuneSigilComponent : Component
{
    [AutoNetworkedField]
    public List<string> Chain = new();

    [AutoNetworkedField]
    public RuneSigilState State;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan Started;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan Changed;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan Stumbled;
}

[Serializable, NetSerializable]
public sealed class RuneImpactEvent : EntityEventArgs
{
    public readonly NetCoordinates Point;
    public readonly string Element;
    public readonly string? Second;
    public readonly float Radius;
    public readonly bool Strong;

    public RuneImpactEvent(NetCoordinates point, string element, string? second, float radius, bool strong)
    {
        Point = point;
        Element = element;
        Second = second;
        Radius = radius;
        Strong = strong;
    }
}
