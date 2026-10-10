using Content.Server.Popups;                 // PopupSystem
using Content.Shared.Sectants;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;

namespace Content.Server.Sectants;

public sealed class SectantFemaleInvisibilityToggleSystem : EntitySystem
{
    [Dependency] private readonly SharedStealthSystem _stealth = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SectantFemaleInvisibilityToggleActionEvent>(OnToggle);
    }

    private void OnToggle(SectantFemaleInvisibilityToggleActionEvent args)
    {
        var uid = args.Performer;
        if (!TryComp<SectantInvisibilityToggleComponent>(uid, out var toggle))
            return;
        if (!TryComp<StealthComponent>(uid, out var stealth))
            return;

        toggle.IsHidden = !toggle.IsHidden;
        var target = toggle.IsHidden ? toggle.HiddenVisibility : toggle.VisibleVisibility;
        _stealth.SetVisibility(uid, target, stealth);

        var key = toggle.IsHidden ? "sectant-female-invisibility-on" : "sectant-female-invisibility-off";
        _popup.PopupEntity(Loc.GetString(key), uid, uid);

        args.Handled = true;
    }
}
