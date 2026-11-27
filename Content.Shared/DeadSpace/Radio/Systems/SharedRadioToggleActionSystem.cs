using Content.Shared.DeadSpace.Radio.Components;
using Content.Shared.Actions;
using Robust.Shared.Containers;
using Content.Shared.Inventory.Events;

namespace Content.Shared.DeadSpace.Radio.Systems;

public abstract partial class SharedRadioToggleActionSystem : EntitySystem
{
    [Dependency] private ActionContainerSystem _actionContainer = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RadioToggleActionComponent, MapInitEvent>(OnMapInit);

        SubscribeLocalEvent<RadioToggleActionComponent, GetItemActionsEvent>(OnGetActions);

        SubscribeLocalEvent<RadioToggleActionComponent, EntGotInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<RadioToggleActionComponent, EntGotRemovedFromContainerMessage>(OnItemRemoved);

        SubscribeLocalEvent<RadioToggleActionMarkerComponent, GotEquippedEvent>(OnMarkerEquipped);
        SubscribeLocalEvent<RadioToggleActionMarkerComponent, GotUnequippedEvent>(OnMarkerUnequipped);
    }

    private void OnMapInit(Entity<RadioToggleActionComponent> ent, ref MapInitEvent args)
    {
        var (uid, comp) = ent;

        _actionContainer.EnsureAction(uid, ref comp.ActionEntity, comp.Action);
        Log.Debug($"[RadioToggle] try EnsureAction {comp.ActionEntity}, Action = {comp.Action}");
    }

    private void OnGetActions(Entity<RadioToggleActionComponent> ent, ref GetItemActionsEvent args)
    {
        var (uid, comp) = ent;

        if (args.InHands)
            return;

        args.AddAction(comp.ActionEntity);
    }

    private void OnInserted(Entity<RadioToggleActionComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        var (uid, comp) = ent;
        if (args.Container.ID != "uniform_accessories")
            return;
        EnsureComp<RadioToggleActionMarkerComponent>(args.Container.Owner).Radio = uid;

        var xform = Transform(args.Container.Owner);

        var wearer = xform.ParentUid;
        if (!wearer.IsValid())
            return;

        _actions.AddAction(wearer, ref ent.Comp.ActionEntity, ent.Comp.Action, uid);
        Dirty(ent.Owner, ent.Comp);
    }

    private void OnItemRemoved(Entity<RadioToggleActionComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        if (args.Container.ID != "uniform_accessories")
            return;

        _actions.RemoveAction(ent.Comp.ActionEntity);
        if (TryComp<RadioToggleActionMarkerComponent>(args.Container.Owner, out var marker))
            RemComp<RadioToggleActionMarkerComponent>(args.Container.Owner);
    }

    private void OnMarkerUnequipped(Entity<RadioToggleActionMarkerComponent> ent, ref GotUnequippedEvent args)
    {
        if (!TryComp<RadioToggleActionComponent>(ent.Comp.Radio, out var radioToggle))
            return;

        _actions.RemoveAction(radioToggle.ActionEntity);
    }
    private void OnMarkerEquipped(Entity<RadioToggleActionMarkerComponent> ent, ref GotEquippedEvent args)
    {
        if (!TryComp<RadioToggleActionComponent>(ent.Comp.Radio, out var radioToggle))
            return;

        _actions.AddAction(args.Equipee, ref radioToggle.ActionEntity, radioToggle.Action, ent.Comp.Radio.Value);
        Dirty(ent.Comp.Radio.Value, radioToggle);
    }
}