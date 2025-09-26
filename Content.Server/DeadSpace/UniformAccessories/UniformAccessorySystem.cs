using Content.Shared.DeadSpace.UniformAccessories;
using Content.Shared.Examine;
using Robust.Shared.Containers;

namespace Content.Server.DeadSpace.UniformAccessories;

public sealed class UniformAccessorySystem : SharedUniformAccessorySystem
{
    private const string ContainerId = "rmc_uniform_accessories";

    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<Shared.DeadSpace.UniformAccessories.Components.UniformAccessoryHolderComponent, EntityTerminatingEvent>(OnTerminating);
        SubscribeLocalEvent<Shared.DeadSpace.UniformAccessories.Components.UniformAccessoryHolderComponent, ExaminedEvent>(OnExamineAccessories);
    }

    private void OnTerminating(EntityUid holder,
        Shared.DeadSpace.UniformAccessories.Components.UniformAccessoryHolderComponent holderComp,
        ref EntityTerminatingEvent eventArgs)
    {
        var container = holderComp.AccessoryContainer;
        if (container == null || container.ContainedEntities.Count == 0)
            return;

        if (!TryComp<TransformComponent>(holder, out var transform))
            return;

        var coordinates = transform.Coordinates;
        foreach (var accessory in container.ContainedEntities)
        {
            if (_container.Remove(accessory, container))
            {
                if (TryComp<TransformComponent>(accessory, out var accessoryTransform))
                    accessoryTransform.Coordinates = coordinates;
            }
        }
    }

    private void OnExamineAccessories(EntityUid holder,
        Shared.DeadSpace.UniformAccessories.Components.UniformAccessoryHolderComponent holderComp,
        ExaminedEvent eventArgs)
    {
        if (!eventArgs.IsInDetailsRange)
            return;

        var container = holderComp.AccessoryContainer;
        if (container == null || container.ContainedEntities.Count == 0)
            return;

        var accessories = new List<string>();
        foreach (var accessory in container.ContainedEntities)
        {
            if (!TryComp<MetaDataComponent>(accessory, out var metaData))
                continue;

            var colorHex = "#FFFF55";
            if (TryComp<Shared.DeadSpace.UniformAccessories.Components.UniformAccessoryComponent>(accessory, out var acc) && acc.Color != null)
                colorHex = acc.Color.Value.ToHex();

            accessories.Add($"[color={colorHex}]{metaData.EntityName}[/color]");
        }

        if (accessories.Count == 0)
            return;

        var accessoriesList = string.Join(", ", accessories);
        eventArgs.PushMarkup($"На этом предмете закреплено: {accessoriesList}.");
    }
}
