// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Alert;
using Content.Shared.DeadSpace.Changeling.Components;

namespace Content.Shared.DeadSpace.Changeling.Systems;

/// <summary>
///     Handles the toggleable part of the Stasis Cocoon ability. The changeling switches the cocoon
///     on and off with a clickable alert on the right side of their screen, right below the health
///     indicator, and this system keeps that alert in sync with <see cref="ChangelingCocoonAbilityComponent.Enabled"/>.
/// </summary>
public sealed class ChangelingCocoonAbilitySystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChangelingCocoonAbilityComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ChangelingCocoonAbilityComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ChangelingCocoonAbilityComponent, ToggleChangelingCocoonEvent>(OnToggle);
        // DS14-start: ComponentHandleState must NOT be subscribed here, the ComponentNetworkGenerator
        // already does that for [AutoGenerateComponentState]. The client picks the alert up from the
        // networked AlertsComponent state that ShowAlert/ClearAlert dirties on the server.
        // DS14-end
    }

    // DS14-start: the ability is granted mid-round by the store, so MapInitEvent has already fired for the buyer.
    private void OnStartup(Entity<ChangelingCocoonAbilityComponent> ent, ref ComponentStartup args)
    {
        UpdateAlert(ent);
    }

    private void OnShutdown(Entity<ChangelingCocoonAbilityComponent> ent, ref ComponentShutdown args)
    {
        _alerts.ClearAlert(ent.Owner, ent.Comp.AlertId);
    }
    // DS14-end

    private void OnToggle(Entity<ChangelingCocoonAbilityComponent> ent, ref ToggleChangelingCocoonEvent args)
    {
        if (args.Handled)
            return;

        SetEnabled(ent, !ent.Comp.Enabled);
        args.Handled = true;
    }

    /// <summary>
    ///     Whether the changeling will be wrapped in a cocoon when entering stasis.
    /// </summary>
    public bool IsEnabled(EntityUid uid)
    {
        return TryComp<ChangelingCocoonAbilityComponent>(uid, out var cocoon) && cocoon.Enabled;
    }

    /// <summary>
    ///     Switches the cocoon on or off, updating the alert.
    /// </summary>
    public void SetEnabled(Entity<ChangelingCocoonAbilityComponent> ent, bool value)
    {
        if (ent.Comp.Enabled == value)
            return;

        ent.Comp.Enabled = value;
        Dirty(ent);
        UpdateAlert(ent);
    }

    private void UpdateAlert(Entity<ChangelingCocoonAbilityComponent> ent)
    {
        _alerts.ShowAlert(ent.Owner, ent.Comp.AlertId, ent.Comp.Enabled ? (short) 1 : (short) 0);
    }
}

/// <summary>
///     Event raised to toggle the Stasis Cocoon ability.
/// </summary>
public sealed partial class ToggleChangelingCocoonEvent : BaseAlertEvent;
