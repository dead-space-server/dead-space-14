// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Sandevistan;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Popups;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.Dodge;

public sealed partial class SharedDodgeSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSandevistanSystem _sandevistan = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DodgeModeImplantComponent, ImplantImplantedEvent>(OnImplanted);
        SubscribeLocalEvent<DodgeModeImplantComponent, ImplantRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<DodgeModeImplantComponent, ComponentShutdown>(OnImplantShutdown);
        SubscribeLocalEvent<DodgeModeImplantComponent, CycleDodgeModeEvent>(OnCycleDodgeMode);
    }

    public bool IsActive(EntityUid uid)
    {
        return _sandevistan.IsActive(uid);
    }

    public float GetChance(Entity<DodgeComponent> ent)
    {
        var chance = IsActive(ent.Owner) ? ent.Comp.ActiveChance : ent.Comp.PassiveChance;
        return Math.Clamp(chance, 0f, 1f);
    }

    private void OnImplanted(Entity<DodgeModeImplantComponent> ent, ref ImplantImplantedEvent args)
    {
        if (!HasComp<DodgeComponent>(args.Implanted))
            return;

        _actions.AddAction(args.Implanted, ref ent.Comp.ModeActionEntity, ent.Comp.ModeAction, ent.Owner);
        Dirty(ent);
        UpdateModeAction(ent, args.Implanted);
    }

    private void OnRemoved(Entity<DodgeModeImplantComponent> ent, ref ImplantRemovedEvent args)
    {
        _actions.RemoveAction(args.Implanted, ent.Comp.ModeActionEntity);
        ent.Comp.ModeActionEntity = null;
        Dirty(ent);
    }

    private void OnImplantShutdown(Entity<DodgeModeImplantComponent> ent, ref ComponentShutdown args)
    {
        if (HostOf(ent.Owner) is { } host)
            _actions.RemoveAction(host, ent.Comp.ModeActionEntity);
    }

    private void OnCycleDodgeMode(Entity<DodgeModeImplantComponent> ent, ref CycleDodgeModeEvent args)
    {
        if (args.Handled || HostOf(ent.Owner) is not { } host || !TryComp<DodgeComponent>(host, out var dodge))
            return;

        dodge.StaminaDodgeMode = !dodge.StaminaDodgeMode;
        Dirty(host, dodge);
        UpdateModeAction(ent, host);

        var popup = dodge.StaminaDodgeMode ? ent.Comp.StaminaPopup : ent.Comp.ChancePopup;
        _popup.PopupPredicted(Loc.GetString(popup), host, host, PopupType.Medium);
        args.Handled = true;
    }

    private void UpdateModeAction(Entity<DodgeModeImplantComponent> ent, EntityUid host)
    {
        if (ent.Comp.ModeActionEntity is not { } action || !TryComp<DodgeComponent>(host, out var dodge))
            return;

        var state = dodge.StaminaDodgeMode ? ent.Comp.StaminaIcon : ent.Comp.ChanceIcon;
        _actions.SetIcon(action, new SpriteSpecifier.Rsi(ent.Comp.ActionRsi, state));
    }

    private EntityUid? HostOf(EntityUid implant)
    {
        if (TryComp<LimbImplantComponent>(implant, out var limb) && limb.Host is { } host && !TerminatingOrDeleted(host))
            return host;

        if (TryComp<SubdermalImplantComponent>(implant, out var subdermal) &&
            subdermal.ImplantedEntity is { } old &&
            !TerminatingOrDeleted(old))
        {
            return old;
        }

        return null;
    }
}
