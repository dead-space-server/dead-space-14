using Content.Server.Implants;
using Content.Shared.DeadSpace.Abductor;
using Content.Shared.Implants.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;

namespace Content.Server.DeadSpace.Abductor;

public sealed partial class AbductorSystem
{
    [Dependency] private readonly SubdermalImplantSystem _subdermalImplant = default!;

    /// <summary>
    /// Lets a loose dubious gland be used directly on a person, implanting it
    /// subdermally without needing a syringe in between.
    /// </summary>
    public void InitializeGland()
    {
        SubscribeLocalEvent<AbductorOrganComponent, AfterInteractEvent>(OnGlandAfterInteract);
    }

    private void OnGlandAfterInteract(Entity<AbductorOrganComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!TryComp<SubdermalImplantComponent>(ent.Owner, out var implantComp)
            || implantComp.ImplantedEntity != null)
            return;

        if (!HasComp<MobStateComponent>(target))
            return;

        _subdermalImplant.ForceImplant(target, (ent.Owner, implantComp));
        _popup.PopupEntity(Loc.GetString("gland-implanted-popup"), target, args.User);
        args.Handled = true;
    }
}