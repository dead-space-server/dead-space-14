using Content.Server.Temperature.Systems;
using Content.Shared.DeadSpace.AshWalkers;
using Content.Shared.Placeable;
using Content.Shared.Temperature.Components;

namespace Content.Server.DeadSpace.AshWalkers;

public sealed class AshWalkerHearthSystem : SharedAshWalkerHearthSystem
{
    [Dependency] private readonly TemperatureSystem _temperature = default!;

    private readonly List<EntityUid> _heatedItems = new();

    protected override void HeatItems(Entity<AshWalkerHearthComponent> ent, float elapsed)
    {
        if (!TryComp<ItemPlacerComponent>(ent, out var placer))
            return;

        _heatedItems.Clear();
        _heatedItems.AddRange(placer.PlacedEntities);
        foreach (var item in _heatedItems)
        {
            if (!TryComp<TemperatureComponent>(item, out var temperature) || temperature.CurrentTemperature >= ent.Comp.MaxTemperature)
                continue;

            var capacity = _temperature.GetHeatCapacity(item, temperature);
            var energy = MathF.Min(ent.Comp.HeatingPower * elapsed, (ent.Comp.MaxTemperature - temperature.CurrentTemperature) * capacity);
            _temperature.ChangeHeat(item, energy, temperature: temperature);
        }
    }
}
