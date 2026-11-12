using Content.Server.DeadSpace.Administration;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;

namespace Content.Server.Implants;

public sealed partial class ImplanterSystem : SharedImplanterSystem
{
    // DS14-start
    /// <summary>
    /// Copies the antag purchase owner from the implanter onto the contained implant before it gets implanted.
    /// Called by <see cref="SharedImplanterSystem"/> through the DS14 pre-implant hook.
    /// </summary>
    protected override void OnBeforeImplant(Entity<ImplanterComponent> ent)
    {
        var contained = ent.Comp.ImplanterSlot.ContainerSlot?.ContainedEntities;
        if (contained == null || contained.Count == 0 ||
            !TryComp<AntagPurchasedEntityComponent>(ent, out var purchase))
        {
            return;
        }

        var implantEntity = contained[0];
        EnsureComp<AntagPurchasedEntityComponent>(implantEntity).MindId = purchase.MindId;
    }
    // DS14-end
}
