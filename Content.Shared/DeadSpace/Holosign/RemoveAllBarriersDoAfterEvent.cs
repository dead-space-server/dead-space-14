// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Holosign;

/// <summary>
/// Raised when the "remove all barriers" action on a limited holosign projector finishes.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class RemoveAllBarriersDoAfterEvent : SimpleDoAfterEvent
{
}
