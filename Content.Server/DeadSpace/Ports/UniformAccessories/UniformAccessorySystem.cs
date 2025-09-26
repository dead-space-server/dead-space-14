using Content.Shared.DeadSpace.Ports.UniformAccessories;
using Content.Shared.DeadSpace.Ports.UniformAccessories.Components;
using Content.Shared.Examine;
using Robust.Shared.Containers;

namespace Content.Server.DeadSpace.Ports.UniformAccessories;

public sealed partial class UniformAccessorySystem : SharedUniformAccessorySystem
{
    private const string ContainerId = "rmc_uniform_accessories";

    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<UniformAccessoryHolderComponent, EntityTerminatingEvent>(OnTerminating);
        SubscribeLocalEvent<UniformAccessoryHolderComponent, ExaminedEvent>(OnExamineAccessories);
    }

    private void OnTerminating(EntityUid holder, UniformAccessoryHolderComponent holderComp, ref EntityTerminatingEvent eventArgs)
    {
        if (!_container.TryGetContainer(holder, ContainerId, out var container) || container.ContainedEntities.Count == 0)
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

    private void OnExamineAccessories(EntityUid holder, UniformAccessoryHolderComponent holderComp, ExaminedEvent eventArgs)
    {
        if (!eventArgs.IsInDetailsRange)
            return;

        if (!_container.TryGetContainer(holder, ContainerId, out var container) || container.ContainedEntities.Count == 0)
            return;

        var accessories = new List<string>();
        foreach (var accessory in container.ContainedEntities)
        {
            if (!TryComp<MetaDataComponent>(accessory, out var metaData))
                continue;

            var colorHex = "#FFFF55";
            if (TryComp<UniformAccessoryComponent>(accessory, out var acc) && acc.Color != null)
                colorHex = ColorToHex(acc.Color.Value);

            accessories.Add($"[color={colorHex}]{metaData.EntityName}[/color]");
        }

        if (accessories.Count == 0)
            return;

        var accessoriesList = string.Join(", ", accessories);
        eventArgs.PushMarkup($"На этом предмете закреплено: {accessoriesList}.");
    }

    private string ColorToHex(Color color)
    {
        var r = (int)MathF.Round(color.R * 255);
        var g = (int)MathF.Round(color.G * 255);
        var b = (int)MathF.Round(color.B * 255);
        return $"#{r:X2}{g:X2}{b:X2}";
    }
}
