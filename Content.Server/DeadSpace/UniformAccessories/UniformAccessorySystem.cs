using System.Linq;
using Content.Shared.DeadSpace.UniformAccessories;
using Content.Shared.DeadSpace.UniformAccessories.Components;
using Robust.Shared.Containers;

namespace Content.Server.DeadSpace.UniformAccessories;

public sealed class UniformAccessorySystem : SharedUniformAccessorySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<UniformAccessoryHolderComponent, EntityTerminatingEvent>(OnTerminating);
    }

    private void OnTerminating(EntityUid holder,
        UniformAccessoryHolderComponent holderComp,
        ref EntityTerminatingEvent args)
    {
        var container = holderComp.AccessoryContainer;
        if (container == null || container.ContainedEntities.Count == 0)
            return;

        var transform = Transform(holder);
        var coordinates = transform.Coordinates;
        var accessories = container.ContainedEntities.ToArray();

        foreach (var accessory in accessories)
        {
            if (!_container.Remove(accessory, container, reparent: false))
                continue;

            if (!coordinates.IsValid(EntityManager) || TerminatingOrDeleted(coordinates.EntityId))
                continue;

            Transform(accessory).Coordinates = coordinates;
        }
    }

}
