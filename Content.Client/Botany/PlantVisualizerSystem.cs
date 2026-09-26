using Content.Client.Botany.Components;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Systems;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;
using Robust.Client.GameObjects;

namespace Content.Client.Botany;

public sealed partial class PlantVisualizerSystem : VisualizerSystem<PlantVisualsComponent>
{
    // DS14-start: current engine uses explicit event subscriptions.
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlantVisualsComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<PlantVisualsComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<PlantComponent, AfterAutoHandleStateEvent>(OnPlantState);
        SubscribeLocalEvent<PlantHolderComponent, AfterAutoHandleStateEvent>(OnHolderState);
        SubscribeLocalEvent<EntityUid>(UpdateSprite);
    }
    // DS14-end

    // DS14-start: current engine uses readonly IoC fields.
    [Dependency] private readonly PlantSystem _plant = default!;
    [Dependency] private readonly PlantHolderSystem _plantHolder = default!;
    [Dependency] private readonly PlantTrayVisualizerSystem _plantTrayVisualizer = default!;
    // DS14-end

    private void OnComponentInit(EntityUid uid, PlantVisualsComponent component, ComponentInit args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        // Ensure they always render above the tray sprite.
        SpriteSystem.SetDrawDepth((uid, sprite), (int)DrawDepth.SmallObjects);
        SpriteSystem.LayerMapReserve((uid, sprite), PlantLayers.Plant);
        SpriteSystem.LayerSetVisible((uid, sprite), PlantLayers.Plant, false);
    }

    private void OnComponentStartup(Entity<PlantVisualsComponent> ent, ref ComponentStartup args)
    {
        UpdateSprite(ent.Owner);
    }

    private void OnPlantState(Entity<PlantComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateSprite(ent.Owner);
    }

    private void OnHolderState(Entity<PlantHolderComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateSprite(ent.Owner);
        if (_plant.TryGetTray(ent.Owner, out var trayEnt))
            _plantTrayVisualizer.UpdateTrayWarnings(trayEnt.AsNullable());
    }

    private void UpdateSprite(EntityUid plantUid)
    {
        if (!HasComp<PlantVisualsComponent>(plantUid)
            || !TryComp<PlantHolderComponent>(plantUid, out var holder) // DS14
            || !TryComp<SpriteComponent>(plantUid, out var sprite))
        {
            return;
        }

        string state;

        var dead = _plantHolder.IsDead(plantUid);
        var harvestReady = holder.ReadyForHarvest; // DS14
        var growthStage = _plant.GetGrowthStageValue(plantUid);

        if (dead)
            state = "dead";
        else if (harvestReady)
            state = "harvest";
        else
            state = $"stage-{growthStage}";

        var layer = SpriteSystem.LayerMapReserve((plantUid, sprite), PlantLayers.Plant);
        SpriteSystem.LayerSetVisible((plantUid, sprite), layer, true);
        SpriteSystem.LayerSetRsiState((plantUid, sprite), layer, state);
    }
}

public enum PlantLayers : byte
{
    Plant
}
