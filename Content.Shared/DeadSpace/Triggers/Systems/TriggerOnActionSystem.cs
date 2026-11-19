using Content.Shared.Actions.Components;
using Content.Shared.Actions;
using Content.Shared.DeadSpace.Triggers.Components;
using Content.Shared.Trigger;

namespace Content.Shared.DeadSpace.Triggers.Systems;

public sealed partial class TriggerOnActionSystem : TriggerOnXSystem
{
    [Dependency] private SharedActionsSystem _action = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TriggerActionEvent>(OnTriggerAction);
    }

    private void OnTriggerAction(TriggerActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<TriggerOnActionComponent>(args.Action, out var comp))
            return;

        if (!TryComp<ActionComponent>(args.Action, out var action))
            return;

        var target = action.Container ?? action.AttachedEntity ?? args.Performer;

        Trigger.Trigger(target, args.Performer, comp.KeyOut);
        args.Handled = true;

        if (comp.RemoveAfterActive)
        {
            _action.RemoveAction((args.Action, action));
            PredictedQueueDel(args.Action);
        }
    }
}
