// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Lavaland;

/// <summary>What the creatures tamed with a lazarus injector are told to do.</summary>
[Serializable, NetSerializable]
public enum LavalandTamedOrder : byte
{
    /// <summary>Walk after the tamer, no fighting.</summary>
    Follow,

    /// <summary>Stand where they are.</summary>
    Stay,

    /// <summary>Fight: whatever the tamer points at first, then any hostile around.</summary>
    Attack,
}

/// <summary>An order for every living creature the performer has tamed.</summary>
public sealed partial class LavalandTamedOrderActionEvent : InstantActionEvent
{
    [DataField(required: true)]
    public LavalandTamedOrder Order;
}
