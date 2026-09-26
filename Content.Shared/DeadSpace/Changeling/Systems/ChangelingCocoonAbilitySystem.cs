// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Alert;
using Content.Shared.DeadSpace.Changeling.Components;

namespace Content.Shared.DeadSpace.Changeling.Systems;

public sealed class ChangelingCocoonAbilitySystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChangelingCocoonAbilityComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ChangelingCocoonAbilityComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ChangelingCocoonAbilityComponent, ToggleChangelingCocoonEvent>(OnToggle);
    }

    private void OnStartup(Entity<ChangelingCocoonAbilityComponent> ent, ref ComponentStartup args)
    {
        UpdateAlert(ent);
    }

    private void OnShutdown(Entity<ChangelingCocoonAbilityComponent> ent, ref ComponentShutdown args)
    {
        _alerts.ClearAlert(ent.Owner, ent.Comp.AlertId);
    }

    private void OnToggle(Entity<ChangelingCocoonAbilityComponent> ent, ref ToggleChangelingCocoonEvent args)
    {
        if (args.Handled)
            return;

        SetEnabled(ent, !ent.Comp.Enabled);
        args.Handled = true;
    }

    public bool IsEnabled(EntityUid uid)
    {
        return TryComp<ChangelingCocoonAbilityComponent>(uid, out var cocoon) && cocoon.Enabled;
    }

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

public sealed partial class ToggleChangelingCocoonEvent : BaseAlertEvent;
