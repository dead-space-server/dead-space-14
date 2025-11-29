using Content.Server.DeadSpace.Components.NightVision;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;

namespace Content.Server.DeadSpace.NightVision;

public sealed class PNVSystem : EntitySystem
{
    public const SlotFlags ValidSlots =
            SlotFlags.HEAD |
            SlotFlags.EYES |
            SlotFlags.MASK
        ;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PNVComponent, GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<PNVComponent, GotUnequippedEvent>(OnGotUnequipped);
    }

    private void OnGotEquipped(EntityUid entity, PNVComponent comp, ref GotEquippedEvent args)
    {
        if ((args.SlotFlags & ValidSlots) == 0)
            return;

        if (HasComp<NightVisionComponent>(args.Equipee))
            return;

        var nightVisionComp = new NightVisionComponent(comp.Color, comp.ActivateSound, comp.Animation);
        comp.HasNightVision = true;

        AddComp(args.Equipee, nightVisionComp);
    }

    private void OnGotUnequipped(EntityUid entity, PNVComponent comp, ref GotUnequippedEvent args)
    {
        if (comp.HasNightVision && HasComp<NightVisionComponent>(args.Equipee))
            RemComp<NightVisionComponent>(args.Equipee);
    }
}
