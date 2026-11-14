// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Buckle.Components;
using Content.Shared.DeadSpace.Surgery;

namespace Content.Server.DeadSpace.Surgery;

public sealed class SurgeryXrayTableSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryXrayTableComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<SurgeryXrayTableComponent, UnstrappedEvent>(OnUnstrapped);
    }

    private void OnStrapped(Entity<SurgeryXrayTableComponent> ent, ref StrappedEvent args)
    {
        ent.Comp.Lit = true;
        Dirty(ent);
    }

    private void OnUnstrapped(Entity<SurgeryXrayTableComponent> ent, ref UnstrappedEvent args)
    {
        ent.Comp.Lit = Patient(ent.Owner) != null;
        Dirty(ent);
    }

    private EntityUid? Patient(EntityUid table)
    {
        var query = EntityQueryEnumerator<BuckleComponent>();
        while (query.MoveNext(out var uid, out var buckle))
        {
            if (buckle.BuckledTo == table)
                return uid;
        }

        return null;
    }
}
