using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.DeadSpace.Implants;
using Content.Shared.Implants;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Implants;

public sealed partial class CritActionCooldownSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<IgnoreCritCooldownComponent, ImplantRelayEvent<MobStateChangedEvent>>(OnMobStateRelay);
        SubscribeLocalEvent<IgnoreCritCooldownComponent, ImplantImplantedEvent>(OnImplantImplanted);
    }

    private void OnMobStateRelay(Entity<IgnoreCritCooldownComponent> ent, ref ImplantRelayEvent<MobStateChangedEvent> args)
    {
        if (args.Event.NewMobState != MobState.Critical && args.Event.NewMobState != MobState.Dead)
            return;

        var implanted = args.ImplantedEntity;
        Timer.Spawn(0, () => RemoveCritActionCooldowns(implanted));
    }

    private void OnImplantImplanted(Entity<IgnoreCritCooldownComponent> ent, ref ImplantImplantedEvent args)
    {
        if (!_mobState.IsCritical(args.Implanted) && !_mobState.IsDead(args.Implanted))
            return;

        RemoveCritActionCooldowns(args.Implanted);
    }

    private void RemoveCritActionCooldowns(EntityUid uid)
    {
        if (Deleted(uid) || Terminating(uid))
            return;

        if (!TryComp<ActionsComponent>(uid, out var actionsComp))
            return;

        foreach (var actionId in actionsComp.Actions)
        {
            if (!TryComp<ActionComponent>(actionId, out var actionComp))
                continue;

            if (actionComp.UseDelay == null)
                continue;

            if (!TryComp<InstantActionComponent>(actionId, out var instant))
                continue;

            if (instant.Event is CritSuccumbEvent or CritLastWordsEvent or CritFakeDeathEvent)
            {
                _actions.SetUseDelay((actionId, actionComp), null);
                _actions.RemoveCooldown((actionId, actionComp));
            }
        }
    }
}
