// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Surgery;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryTankMountComponent : Component
{
    [DataField]
    public string Slot = "surgery_tank";
}

[ByRefEvent]
public record struct FindInternalsTankEvent(Entity<GasTankComponent>? Tank = null);

[ByRefEvent]
public record struct GasTankBreatherEvent(EntityUid? Breather = null);

public sealed class SurgeryTankMountSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedInternalsSystem _internals = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<InternalsComponent, FindInternalsTankEvent>(OnFind);
        SubscribeLocalEvent<GasTankComponent, GasTankBreatherEvent>(OnBreather);
        SubscribeLocalEvent<SurgeryTankMountComponent, UnstrappedEvent>(OnUnstrapped);
    }

    private void OnFind(Entity<InternalsComponent> ent, ref FindInternalsTankEvent args)
    {
        if (args.Tank != null
            || !TryComp<BuckleComponent>(ent, out var buckle)
            || buckle.BuckledTo is not { } strap
            || Tank(strap) is not { } tank)
            return;

        args.Tank = tank;
    }

    private void OnBreather(Entity<GasTankComponent> ent, ref GasTankBreatherEvent args)
    {
        if (args.Breather != null
            || !_containers.TryGetContainingContainer((ent.Owner, Transform(ent.Owner)), out var container)
            || !HasComp<SurgeryTankMountComponent>(container.Owner)
            || !TryComp<StrapComponent>(container.Owner, out var strap))
            return;

        foreach (var buckled in strap.BuckledEntities)
        {
            if (!HasComp<InternalsComponent>(buckled))
                continue;

            args.Breather = buckled;
            return;
        }
    }

    private void OnUnstrapped(Entity<SurgeryTankMountComponent> ent, ref UnstrappedEvent args)
    {
        var patient = args.Buckle.Owner;
        if (!TryComp<InternalsComponent>(patient, out var internals)
            || Tank(ent) is not { } tank
            || internals.GasTankEntity != tank.Owner)
            return;

        _internals.DisconnectTank((patient, internals), forced: true);
    }

    private Entity<GasTankComponent>? Tank(EntityUid strap)
    {
        if (!TryComp<SurgeryTankMountComponent>(strap, out var mount)
            || !_slots.TryGetSlot(strap, mount.Slot, out var slot)
            || slot.Item is not { } item
            || !TryComp<GasTankComponent>(item, out var tank))
            return null;

        return (item, tank);
    }
}
