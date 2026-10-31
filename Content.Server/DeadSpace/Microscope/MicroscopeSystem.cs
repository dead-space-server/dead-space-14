// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using System.Linq;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeadSpace.Microscope;
using Content.Shared.DeadSpace.OrganDamage;
using Content.Shared.FixedPoint;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Server.DeadSpace.Microscope;

public sealed class MicroscopeSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MicroscopeComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<MicroscopeComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<MicroscopeComponent, MicroscopeActionMessage>(OnAction);
        SubscribeLocalEvent<MicroscopeComponent, AfterActivatableUIOpenEvent>(OnUiOpen);
    }

    private void OnUiOpen(Entity<MicroscopeComponent> ent, ref AfterActivatableUIOpenEvent args)
    {
        UpdateUi(ent);
    }

    private void OnInserted(Entity<MicroscopeComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.SlotId)
            return;

        _audio.PlayPvs(ent.Comp.InsertSound, ent.Owner);
        UpdateUi(ent);
    }

    private void OnRemoved(Entity<MicroscopeComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.SlotId)
            return;

        _audio.PlayPvs(ent.Comp.EjectSound, ent.Owner);
        UpdateUi(ent);
    }

    private void OnAction(Entity<MicroscopeComponent> ent, ref MicroscopeActionMessage args)
    {
        if (args.Action != MicroscopeUiAction.EjectOrgan)
            return;

        if (_itemSlots.TryGetSlot(ent.Owner, ent.Comp.SlotId, out var ejectSlot))
            _itemSlots.TryEjectToHands(ent.Owner, ejectSlot, args.Actor);

        UpdateUi(ent);
    }

    private void UpdateUi(Entity<MicroscopeComponent> ent)
    {
        if (!_ui.HasUi(ent.Owner, MicroscopeUiKey.Key))
            return;

        _ui.SetUiState(ent.Owner, MicroscopeUiKey.Key, BuildState(ent));
    }

    private MicroscopeBoundUserInterfaceState BuildState(Entity<MicroscopeComponent> ent)
    {
        EntityUid? organ = null;

        if (_itemSlots.TryGetSlot(ent.Owner, ent.Comp.SlotId, out var slot) &&
            slot.ContainerSlot?.ContainedEntity is { Valid: true } contained)
        {
            organ = contained;
        }

        if (organ == null)
            return new MicroscopeBoundUserInterfaceState(null, string.Empty, FixedPoint2.Zero, FixedPoint2.Zero, false, new List<OrganWound>());

        TryComp<OrganDamageableComponent>(organ, out var organDamage);

        var damage = organDamage?.Damage ?? FixedPoint2.Zero;
        var maxDamage = organDamage?.MaxDamage ?? FixedPoint2.Zero;
        var dead = organDamage?.Dead ?? false;
        var wounds = organDamage?.Wounds.ToList() ?? new List<OrganWound>();
        var organName = TryComp<MetaDataComponent>(organ, out var meta) ? meta.EntityName : string.Empty;

        return new MicroscopeBoundUserInterfaceState(
            GetNetEntity(organ.Value),
            organName,
            damage,
            maxDamage,
            dead,
            wounds);
    }
}
