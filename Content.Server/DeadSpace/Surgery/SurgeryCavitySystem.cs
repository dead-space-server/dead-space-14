// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.Gibbing;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Surgery;

public sealed class SurgeryCavitySystem : EntitySystem
{
    private static readonly ProtoId<SurgeryLodgePrototype> Lodge = "Lodge";
    private static readonly ProtoId<ItemSizePrototype> Tiny = "Tiny";
    private static readonly ProtoId<ItemSizePrototype> Small = "Small";
    private static readonly ProtoId<ItemSizePrototype> Normal = "Normal";

    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedItemSystem _items = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LimbBodyComponent, ComponentStartup>(OnBody);
        SubscribeLocalEvent<SurgeryCavityComponent, BeingGibbedEvent>(OnGib);
    }

    private void OnBody(Entity<LimbBodyComponent> ent, ref ComponentStartup args)
    {
        EnsureComp<SurgeryCavityComponent>(ent);
        Open(ent, SurgeryCavityComponent.Head);
        Open(ent, SurgeryCavityComponent.Abdomen);
        Open(ent, SurgeryCavityComponent.Chest);
    }

    private void Open(EntityUid body, string id)
    {
        var pocket = _containers.EnsureContainer<Container>(body, id);
        var show = id == SurgeryCavityComponent.Abdomen;
        if (pocket.ShowContents == show)
            return;

        pocket.ShowContents = show;
        if (TryComp<ContainerManagerComponent>(body, out var manager))
            Dirty(body, manager);
    }

    private void OnGib(Entity<SurgeryCavityComponent> ent, ref BeingGibbedEvent args)
    {
        Spill(ent, SurgeryCavityComponent.Head, args.Giblets);
        Spill(ent, SurgeryCavityComponent.Chest, args.Giblets);
        Spill(ent, SurgeryCavityComponent.Abdomen, args.Giblets);
    }

    private void Spill(EntityUid body, string id, HashSet<EntityUid> dropped)
    {
        if (!_containers.TryGetContainer(body, id, out var cavity))
            return;

        foreach (var item in cavity.ContainedEntities)
            dropped.Add(item);
    }

    public bool Blocked(EntityUid item)
    {
        if (!_prototypes.TryIndex(Lodge, out SurgeryLodgePrototype? lodge))
            return false;

        foreach (var name in lodge.Blocked)
        {
            if (!_factory.TryGetRegistration(name, out var registration))
                continue;

            if (HasComp(item, registration.Type))
                return true;
        }

        return false;
    }

    public bool Fits(LimbZone zone, EntityUid item)
    {
        return !Blocked(item)
               && Pocket(zone, out _, out var max)
               && TryComp<ItemComponent>(item, out var held)
               && _items.GetItemSizeWeight(held.Size) <= _items.GetItemSizeWeight(max);
    }

    public bool TryLodge(EntityUid patient, LimbZone zone, EntityUid item)
    {
        if (!Fits(zone, item) || !Pocket(zone, out var id, out _))
            return false;

        if (!_containers.TryGetContainer(patient, id, out var cavity))
            return false;

        if (cavity.ContainedEntities.Count >= SurgeryCavityComponent.Cap)
            return false;

        if (!_containers.Insert(item, cavity))
            return false;

        Remember(patient, zone, item);
        return true;
    }

    public bool TryWithdraw(EntityUid patient, LimbZone zone, EntityUid user)
    {
        if (!Pocket(zone, out var id, out _) || !_containers.TryGetContainer(patient, id, out var cavity))
            return false;

        if (cavity.ContainedEntities.Count == 0)
            return false;

        var item = cavity.ContainedEntities[0];
        if (!_containers.Remove(item, cavity))
            return false;

        Remember(patient, zone, cavity.ContainedEntities.Count == 0 ? null : cavity.ContainedEntities[0]);
        _hands.TryPickupAnyHand(user, item);
        return true;
    }

    private void Remember(EntityUid patient, LimbZone zone, EntityUid? item)
    {
        if (!TryComp<SurgeryCavityComponent>(patient, out var mark))
            return;

        var net = item == null ? NetEntity.Invalid : GetNetEntity(item.Value);
        var proto = item == null ? "" : Prototype(item.Value)?.ID ?? "";
        switch (zone)
        {
            case LimbZone.Head:
                mark.HeadItem = net;
                mark.HeadProto = proto;
                break;
            case LimbZone.Groin:
                mark.AbdomenItem = net;
                mark.AbdomenProto = proto;
                break;
            case LimbZone.Torso:
                mark.ChestItem = net;
                mark.ChestProto = proto;
                break;
        }

        Dirty(patient, mark);
    }

    private static bool Pocket(LimbZone zone, out string id, out ProtoId<ItemSizePrototype> max)
    {
        switch (zone)
        {
            case LimbZone.Head:
                id = SurgeryCavityComponent.Head;
                max = Tiny;
                return true;
            case LimbZone.Groin:
                id = SurgeryCavityComponent.Abdomen;
                max = Small;
                return true;
            case LimbZone.Torso:
                id = SurgeryCavityComponent.Chest;
                max = Normal;
                return true;
            default:
                id = "";
                max = default;
                return false;
        }
    }
}
