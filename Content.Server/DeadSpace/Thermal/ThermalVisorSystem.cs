// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.ThermalVision;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;

namespace Content.Server.DeadSpace.ThermalVision;

public sealed class ThermalVisorSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ThermalVisorComponent, GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<ThermalVisorComponent, GotUnequippedEvent>(OnGotUnequipped);
    }

    private void OnGotEquipped(EntityUid entity, ThermalVisorComponent comp, ref GotEquippedEvent args)
    {
        if ((args.SlotFlags & comp.ValidSlots) == 0)
            return;

        if (HasComp<ThermalVisionComponent>(args.Equipee))
            return;

        GiveThermalVision(entity, comp, args.Equipee);
    }

    private void OnGotUnequipped(EntityUid entity, ThermalVisorComponent comp, ref GotUnequippedEvent args)
    {
        if (!comp.HasThermalVision)
            return;

        if (!TryComp<ThermalVisionComponent>(args.Equipee, out var vision))
            return;

        if (vision.GrantedBy != entity)
            return;

        RemComp<ThermalVisionComponent>(args.Equipee);

        comp.HasThermalVision = false;

        ReapplyThermalVision(args.Equipee);
    }

    private void GiveThermalVision(EntityUid visor, ThermalVisorComponent visorComp, EntityUid wearer)
    {
        var activeComp = new ThermalVisionComponent
        {
            GrantedBy = visor,
            ActivateSound = visorComp.ActivateSound,
            ActivateSoundOff = visorComp.ActivateSoundOff,
            Animation = visorComp.Animation,
            UseShader = visorComp.UseShader
        };

        visorComp.HasThermalVision = true;

        AddComp(wearer, activeComp);
    }

    private void ReapplyThermalVision(EntityUid wearer)
    {
        var slots = _inventory.GetSlotEnumerator(wearer);

        while (slots.NextItem(out var item, out var slotDefinition))
        {
            if (!TryComp<ThermalVisorComponent>(item, out var visor))
                continue;

            if ((visor.ValidSlots & slotDefinition.SlotFlags) == 0)
                continue;

            GiveThermalVision(item, visor, wearer);
            return;
        }
    }
}