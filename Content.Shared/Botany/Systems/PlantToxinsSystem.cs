using JetBrains.Annotations;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Events;

namespace Content.Shared.Botany.Systems;

/// <summary>
/// Handles toxin accumulation and tolerance for plants, applying health damage
/// and decrementing toxins based on per-tick uptake.
/// </summary>
public sealed partial class PlantToxinsSystem : EntitySystem
{
    // DS14-start: current engine uses explicit event subscriptions and query initialization.
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlantToxinsComponent, PlantCrossPollinateEvent>(OnCrossPollinate);
        SubscribeLocalEvent<PlantToxinsComponent, PlantGrowEvent>(OnPlantGrow);
        _trayQuery = GetEntityQuery<PlantTrayComponent>();
        _holderQuery = GetEntityQuery<PlantHolderComponent>();
    }
    // DS14-end

    // DS14-start
    [Dependency] private readonly BotanySystem _botany = default!;
    [Dependency] private readonly PlantMutationSystem _mutation = default!;
    [Dependency] private readonly PlantHolderSystem _plantHolder = default!;
    [Dependency] private readonly PlantTraySystem _plantTray = default!;

    private EntityQuery<PlantTrayComponent> _trayQuery;
    private EntityQuery<PlantHolderComponent> _holderQuery;
    // DS14-end

    private void OnCrossPollinate(Entity<PlantToxinsComponent> ent, ref PlantCrossPollinateEvent args)
    {
        if (!_botany.TryGetPlantComponent<PlantToxinsComponent>(args.PollenData, args.PollenProtoId, out var pollenData))
            return;

        // DS14-start
        _mutation.CrossFloat(ref ent.Comp.ToxinsTolerance, pollenData.ToxinsTolerance);
        _mutation.CrossFloat(ref ent.Comp.ToxinUptakeDivisor, pollenData.ToxinUptakeDivisor);
        // DS14-end
        Dirty(ent);
    }

    private void OnPlantGrow(Entity<PlantToxinsComponent> ent, ref PlantGrowEvent args)
    {
        var trayUid = GetEntity(args.Tray);
        if (!_trayQuery.TryComp(trayUid, out var tray)
            || !_holderQuery.TryComp(ent.Owner, out var holder)) // DS14
            return;

        if (ent.Comp.ToxinUptakeDivisor <= 0)
            return;

        var toxinUptake = MathF.Max(1, MathF.Round(tray.ToxinLevel / ent.Comp.ToxinUptakeDivisor));
        if (tray.ToxinLevel > ent.Comp.ToxinsTolerance)
        {
            // Get minimum value between health left and toxin uptake.
            var actualUptake = Math.Min(toxinUptake, holder.Health);

            _plantHolder.AdjustsHealth(ent.Owner, -actualUptake);
            _plantTray.AdjustToxin((trayUid, tray), -actualUptake);
        }
        else
        {
            _plantTray.AdjustToxin((trayUid, tray), -toxinUptake);
        }
    }

    /// <summary>
    /// Adjusts maximum toxin level the plant can tolerate before taking damage.
    /// </summary>
    [PublicAPI]
    public void AdjustToxinsTolerance(Entity<PlantToxinsComponent?> ent, float amount)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        ent.Comp.ToxinsTolerance = MathF.Max(0f, ent.Comp.ToxinsTolerance + amount);
        DirtyField(ent, nameof(ent.Comp.ToxinsTolerance));
    }
}
