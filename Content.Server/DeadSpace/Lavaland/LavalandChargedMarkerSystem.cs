// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Lavaland;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// The kinetic machete's marker shot on the server: besides the shared handling, a charge held past the limit blows up
/// without waiting for the button.
/// </summary>
public sealed class LavalandChargedMarkerSystem : SharedLavalandChargedMarkerSystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = Timing.CurTime;
        var query = EntityQueryEnumerator<LavalandChargedMarkerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (!comp.Charging || now - comp.ChargeStart < comp.OverchargeTime)
                continue;

            if (xform.ParentUid is { Valid: true } holder && Hands.IsHolding(holder, uid))
            {
                Overcharge(holder, (uid, comp));
                continue;
            }

            comp.Charging = false;
            Dirty(uid, comp);
        }
    }
}
