// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Access.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeadSpace.Kitchen.Components;
using Content.Shared.PDA; // DS14
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;

namespace Content.Server.DeadSpace.Pvs;

public sealed partial class PvsDetailSystem : EntitySystem
{
    private const string PocketPrefix = "pocket";

    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private MetaDataSystem _meta = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ContainedSolutionComponent, ComponentStartup>(OnSolutionStartup);
        SubscribeLocalEvent<ContainerManagerComponent, MapInitEvent>(OnContainersMapInit);
        SubscribeLocalEvent<EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<EntRemovedFromContainerMessage>(OnRemoved);
    }

    private void OnSolutionStartup(Entity<ContainedSolutionComponent> ent, ref ComponentStartup args)
    {
        _meta.AddFlag(ent, MetaDataFlags.PvsDetail);
    }

    private void OnContainersMapInit(Entity<ContainerManagerComponent> ent, ref MapInitEvent args)
    {
        foreach (var container in ent.Comp.Containers.Values)
        {
            if (!IsHidden(container))
                continue;

            foreach (var contained in container.ContainedEntities)
            {
                if (!IsPdaIdCard(container, contained)) // DS14
                    _meta.AddFlag(contained, MetaDataFlags.PvsDetail);
            }
        }
    }

    private void OnInserted(EntInsertedIntoContainerMessage args)
    {
        if (IsHidden(args.Container) && !IsPdaIdCard(args.Container, args.Entity))
            _meta.AddFlag(args.Entity, MetaDataFlags.PvsDetail);
    }

    private void OnRemoved(EntRemovedFromContainerMessage args)
    {
        if (IsHidden(args.Container) && !HasComp<ContainedSolutionComponent>(args.Entity))
            _meta.RemoveFlag(args.Entity, MetaDataFlags.PvsDetail);
    }

    // DS14-Start
    private bool IsPdaIdCard(BaseContainer container, EntityUid entity)
    {
        return HasComp<IdCardComponent>(entity) &&
               container.ID == PdaComponent.PdaIdSlotId &&
               HasComp<PdaComponent>(container.Owner);
    }
    // DS14-End

    private bool IsHidden(BaseContainer container)
    {
        var id = container.ID;
        if (id == SharedEntityStorageSystem.ContainerName ||
            id == StorageComponent.ContainerId ||
            id == SharedBodySystem.BodyRootContainerId ||
            id.StartsWith(SharedBodySystem.PartSlotContainerIdPrefix, StringComparison.Ordinal) ||
            id.StartsWith(SharedBodySystem.OrganSlotContainerIdPrefix, StringComparison.Ordinal) ||
            id.StartsWith(PocketPrefix, StringComparison.Ordinal))
        {
            return true;
        }

        return TryComp<ItemSlotsComponent>(container.Owner, out var slots) &&
               _itemSlots.TryGetSlot(container.Owner, id, out _, slots) &&
               !HasComp<PlateComponent>(container.Owner);
    }
}
