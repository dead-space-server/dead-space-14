// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions.Components;
using Content.Shared.Changeling.Components;

namespace Content.Shared.DeadSpace.Changeling.Systems;

public sealed class ChangelingHeadlessSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChangelingIdentityComponent, BrainMindTransferAttemptEvent>(OnBrainLeaves);
    }

    private void OnBrainLeaves(Entity<ChangelingIdentityComponent> ent, ref BrainMindTransferAttemptEvent args)
    {
        if (Stasis(ent.Owner) != null)
            args.Cancelled = true;
    }

    private Entity<RegenerativeStasisActionComponent>? Stasis(EntityUid uid)
    {
        if (!TryComp<ActionsComponent>(uid, out var actions))
            return null;

        foreach (var action in actions.Actions)
        {
            if (TryComp<RegenerativeStasisActionComponent>(action, out var stasis))
                return (action, stasis);
        }

        return null;
    }
}

[ByRefEvent]
public record struct BrainMindTransferAttemptEvent(EntityUid Brain, bool Cancelled = false);
