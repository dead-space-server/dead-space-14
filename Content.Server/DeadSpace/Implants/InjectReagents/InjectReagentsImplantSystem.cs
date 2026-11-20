// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DeadSpace.Implants.Components;
using Content.Shared.Implants.Components;
using Content.Shared.Popups;

namespace Content.Server.DeadSpace.Implants.InjectReagents;

public sealed partial class InjectReagentsImplantSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InjectReagentsImplantComponent, UseInjectReagentsImplantEvent>(OnActivated);
    }

    private void OnActivated(EntityUid uid, InjectReagentsImplantComponent component, UseInjectReagentsImplantEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<SubdermalImplantComponent>(uid, out var subdermal) || subdermal.ImplantedEntity is not { } target)
            return;

        if (!_solution.TryGetInjectableSolution(target, out var injectable, out _))
            return;

        var injected = false;
        foreach (var (reagent, quantity) in component.Reagents)
        {
            if (quantity <= 0)
                continue;

            injected |= _solution.TryAddReagent(injectable.Value, reagent, quantity, out _);
        }

        if (!injected)
            return;

        if (component.Popup is { } popup)
            _popup.PopupEntity(Loc.GetString(popup), target, target);

        args.Handled = true;
    }
}
