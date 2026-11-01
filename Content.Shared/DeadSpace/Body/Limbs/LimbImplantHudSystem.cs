// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.Popups;

namespace Content.Shared.DeadSpace.Body.Limbs;

public sealed partial class ToggleLimbImplantEvent : InstantActionEvent;

public sealed class LimbImplantHudSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ToggleLimbImplantEvent>(OnToggle);
    }

    private void OnToggle(ToggleLimbImplantEvent args)
    {
        if (args.Handled)
            return;

        if (args.Action.Comp.Container is not { } item
            || !TryComp<LimbImplantComponent>(item, out var implant)
            || implant.Host != args.Performer)
        {
            _popup.PopupClient(Loc.GetString("surgery-implant-hud-missing"), args.Performer, args.Performer);
            return;
        }

        args.Handled = true;
        SetToggled((item, implant), args.Performer, !implant.Toggled);
        _popup.PopupClient(Loc.GetString(implant.Toggled ? "surgery-implant-hud-on" : "surgery-implant-hud-off"), args.Performer, args.Performer);
    }

    public void SetToggled(Entity<LimbImplantComponent> implant, EntityUid host, bool on)
    {
        if (implant.Comp.Toggled == on)
            return;

        if (on)
            EntityManager.AddComponents(host, implant.Comp.ToggleComponents);
        else
            EntityManager.RemoveComponents(host, implant.Comp.ToggleComponents);

        implant.Comp.Toggled = on;
        Dirty(implant);
    }
}
