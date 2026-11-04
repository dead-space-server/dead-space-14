// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Shared.Body.Part;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Surgery;

[Serializable, NetSerializable]
public enum SurgeryAction : byte
{
    Incise,
    Extract,
    Insert,
}

[Serializable, NetSerializable]
public sealed partial class SurgeryDoAfterEvent : DoAfterEvent
{
    [DataField]
    public SurgeryAction Action;

    [DataField]
    public BodyPartType Zone;

    public override DoAfterEvent Clone()
    {
        return new SurgeryDoAfterEvent { Action = Action, Zone = Zone };
    }
}
