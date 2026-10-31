// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Body.Limbs;

[Serializable, NetSerializable]
public sealed partial class LimbHarvestDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public LimbZone Zone;

    [DataField]
    public bool Extract;

    public LimbHarvestDoAfterEvent()
    {
    }

    public LimbHarvestDoAfterEvent(LimbZone zone, bool extract)
    {
        Zone = zone;
        Extract = extract;
    }

    public override DoAfterEvent Clone()
    {
        return new LimbHarvestDoAfterEvent(Zone, Extract);
    }
}
