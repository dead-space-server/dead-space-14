using Robust.Shared.Containers;

using Content.Shared.Item;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Toggleable;

namespace Content.Shared.ContainerHeld;

public sealed class ContainerHeldSystem : EntitySystem
{
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ContainerHeldComponent, EntInsertedIntoContainerMessage>(OnContainerModified);
        SubscribeLocalEvent<ContainerHeldComponent, EntRemovedFromContainerMessage>(OnContainerModified);
    }

    private void OnContainerModified(EntityUid uid, ContainerHeldComponent comp, ContainerModifiedMessage args)
    {
        if (!(TryComp<StorageComponent>(uid, out var storage) // DS14
              && TryComp<AppearanceComponent>(uid, out var appearance)
              && TryComp<ItemComponent>(uid, out var item)))
        {
            return;
        }

        // DS14-start: a pre-filled storage can get its contents in the client's state before its container is set up.
        if (storage.Container == null)
            return;
        // DS14-end
        if (_storage.GetCumulativeItemAreas(uid) >= comp.Threshold)
        {
            _item.SetHeldPrefix(uid, "full", component: item);
            _appearance.SetData(uid, ToggleableVisuals.Enabled, true, appearance);
        }
        else
        {
            _item.SetHeldPrefix(uid, "empty", component: item);
            _appearance.SetData(uid, ToggleableVisuals.Enabled, false, appearance);
        }
    }
}
