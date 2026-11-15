// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Pointing;

[Serializable, NetSerializable]
public sealed class PointingBubbleEvent : EntityEventArgs
{
    public NetEntity Pointer;
    public NetEntity Target;
    public TimeSpan EndTime;

    public PointingBubbleEvent(NetEntity pointer, NetEntity target, TimeSpan endTime)
    {
        Pointer = pointer;
        Target = target;
        EndTime = endTime;
    }
}
