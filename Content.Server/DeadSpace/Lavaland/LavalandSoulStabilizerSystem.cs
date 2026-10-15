// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// Stabilizing serum: injected into a legion soul, it stops the soul from decaying into an inert husk.
/// </summary>
public sealed class LavalandSoulStabilizerSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandSoulStabilizerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<LavalandStabilizableComponent, ExaminedEvent>(OnExamine);
    }

    private void OnAfterInteract(Entity<LavalandSoulStabilizerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        args.Handled = true;
        if (!TryComp<LavalandStabilizableComponent>(target, out var stabilizable))
        {
            _popup.PopupEntity(Loc.GetString("lavaland-stabilizer-invalid"), target, args.User);
            return;
        }

        if (stabilizable.Stabilized)
        {
            _popup.PopupEntity(Loc.GetString("lavaland-stabilizer-already"), target, args.User);
            return;
        }

        RemComp<PerishableComponent>(target);
        RemComp<RotIntoComponent>(target);
        stabilizable.Stabilized = true;
        _popup.PopupEntity(Loc.GetString("lavaland-stabilizer-success"), target, args.User);
        QueueDel(ent);
    }

    private void OnExamine(Entity<LavalandStabilizableComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.Stabilized
            ? "lavaland-stabilizer-examine-stable"
            : "lavaland-stabilizer-examine-decaying"));
    }
}
