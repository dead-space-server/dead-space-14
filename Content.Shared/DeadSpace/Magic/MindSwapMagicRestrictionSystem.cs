using System.Linq;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.DeadSpace.Magic.Components;
using Content.Shared.Magic;
using Content.Shared.Magic.Events;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;

namespace Content.Shared.DeadSpace.Magic;

public sealed partial class MindSwapMagicRestrictionSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MindSwapSpellEvent>(OnMindSwapSpell, after: new[] { typeof(SharedMagicSystem) });
        SubscribeLocalEvent<MindContainerComponent, MindAddedMessage>(OnMindAdded);
    }

    private void OnMindSwapSpell(MindSwapSpellEvent ev)
    {
        if (!ev.Handled)
            return;

        if (_mind.TryGetMind(ev.Target, out var perfMindId, out _))
            StripRestrictedActionsIfNeeded(perfMindId, ev.Target);

        if (_mind.TryGetMind(ev.Performer, out var tarMindId, out _))
            StripRestrictedActionsIfNeeded(tarMindId, ev.Performer);
    }

    private void OnMindAdded(Entity<MindContainerComponent> body, ref MindAddedMessage args)
    {
        var mindId = args.Mind.Owner;

        if (HasComp<CantGetMagicOnSwapComponent>(body))
        {
            StripRestrictedActionsIfNeeded(mindId, body);
        }
        else
        {
            RestoreHeldBackActions(mindId, body);
        }
    }

    private void StripRestrictedActionsIfNeeded(EntityUid mindId, EntityUid body)
    {
        if (!HasComp<CantGetMagicOnSwapComponent>(body))
            return;

        if (!TryComp<ActionsComponent>(body, out var actionsComp))
            return;

        foreach (var actionId in actionsComp.Actions.ToArray())
        {
            if (!HasComp<NonSwapMagicComponent>(actionId))
                continue;

            _actions.RemoveAction((body, actionsComp), actionId);
        }
    }

    private void RestoreHeldBackActions(EntityUid mindId, EntityUid body)
    {
        if (!TryComp<ActionsContainerComponent>(mindId, out var container))
            return;

        foreach (var actionId in container.Container.ContainedEntities.ToArray())
        {
            if (!HasComp<NonSwapMagicComponent>(actionId))
                continue;

            if (TryComp<ActionComponent>(actionId, out var actionComp) && actionComp.AttachedEntity == body)
                continue;

            _actions.AddActionDirect((body, null), actionId);
        }
    }
}
