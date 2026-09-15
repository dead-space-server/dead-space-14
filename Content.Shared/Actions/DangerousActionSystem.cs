using Content.Shared.Actions.Components;
using Content.Shared.Actions.Events;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Popups;

namespace Content.Shared.Actions;

public sealed partial class DangerousActionSystem : EntitySystem
{
    private EntityQuery<PacifiedComponent> _pacifiedQuery; // DS14 - initialized explicitly on the current engine.
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        _pacifiedQuery = GetEntityQuery<PacifiedComponent>(); // DS14
        SubscribeLocalEvent<DangerousActionComponent, ActionAttemptEvent>(OnAttempt);
    }

    private void OnAttempt(Entity<DangerousActionComponent> ent, ref ActionAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!_pacifiedQuery.HasComp(args.User))
            return;

        // DS14: the legacy ActionAttemptEvent has no popup payload.
        _popup.PopupPredicted(Loc.GetString(ent.Comp.PacificationMessage), args.User, args.User, ent.Comp.MessageType);
        args.Cancelled = true;
    }

}
