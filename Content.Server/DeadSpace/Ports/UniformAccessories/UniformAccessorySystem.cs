using System.Linq;
using Content.Shared.DeadSpace.Ports.UniformAccessories;
using Content.Shared.DeadSpace.Ports.UniformAccessories.Components;
using Content.Shared.Destructible;
using Content.Shared.Examine;
using Robust.Shared.Containers;

namespace Content.Server.DeadSpace.Ports.UniformAccessories;

public sealed class UniformAccessorySystem : SharedUniformAccessorySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UniformAccessoryHolderComponent, ExaminedEvent>(OnExamineAccessories);
        SubscribeLocalEvent<UniformAccessoryHolderComponent, DestructionEventArgs>(OnDestroyed);
        SubscribeLocalEvent<UniformAccessoryHolderComponent, EntityTerminatingEvent>(OnTerminating);
    }

    private void OnDestroyed(EntityUid uid, UniformAccessoryHolderComponent component, DestructionEventArgs args)
    {
        DropAllAccessories(uid, component);
    }

    private void OnTerminating(EntityUid uid,
        UniformAccessoryHolderComponent component,
        ref EntityTerminatingEvent args)
    {
        DropAllAccessories(uid, component);
    }

    private void DropAllAccessories(EntityUid uid, UniformAccessoryHolderComponent component)
    {
        if (!_container.TryGetContainer(uid, component.ContainerId, out var container) || container.Count == 0)
            return;

        var coordinates = Transform(uid).Coordinates;
        var accessories = container.ContainedEntities.ToList();

        foreach (var accessory in accessories)
        {
            if (_container.Remove(accessory, container))
                Transform(accessory).Coordinates = coordinates;
        }
    }

    private void OnExamineAccessories(EntityUid uid, UniformAccessoryHolderComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (!_container.TryGetContainer(uid, component.ContainerId, out var container) || container.Count == 0)
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
        args.PushMarkup($"На этом предмете закреплено: {accessoriesList}.");
    }

    private string ColorToHex(Color c)
    {
        var r = (int)MathF.Round(c.R * 255);
        var g = (int)MathF.Round(c.G * 255);
        var b = (int)MathF.Round(c.B * 255);
        return $"#{r:X2}{g:X2}{b:X2}";
    }
}
