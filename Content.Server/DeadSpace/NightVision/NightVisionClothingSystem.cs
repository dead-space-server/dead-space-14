using Content.Server.DeadSpace.Components.NightVision;
using Content.Shared.Inventory.Events;

namespace Content.Server.DeadSpace.NightVision;

public sealed class NightVisionClothingSystem : EntitySystem
{

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NightVisionClothingComponent, GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<NightVisionClothingComponent, GotUnequippedEvent>(OnGotUnequipped);
    }

    private void OnGotEquipped(EntityUid entity, NightVisionClothingComponent comp, ref GotEquippedEvent args)
    {
        if (HasComp<NightVisionComponent>(args.Equipee))
            return;

        var nightVisionComp = new NightVisionComponent();
        comp.HasNightVision = true;
        AddComp(args.Equipee, nightVisionComp);
    }

    private void OnGotUnequipped(EntityUid entity, NightVisionClothingComponent comp, ref GotUnequippedEvent args)
    {
        if (comp.HasNightVision && HasComp<NightVisionComponent>(args.Equipee))
            RemComp<NightVisionComponent>(args.Equipee);
    }
}
