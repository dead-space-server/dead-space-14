using Content.Shared.Botany.Components;
using Content.Shared.Botany.Events;
using Content.Shared.Botany.Systems;
using Content.Shared.Botany.Traits.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;

namespace Content.Shared.Botany.Traits.Systems;

/// <inheritdoc cref="PlantTraitLigneousComponent"/>
public sealed partial class PlantTraitLigneousSystem : EntitySystem
{
    // DS14-start
    [Dependency] private readonly PlantHarvestSystem _plantHarvest = default!;
    [Dependency] private readonly PlantHolderSystem _plantHolder = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;

    private EntityQuery<PlantHolderComponent> _holderQuery;
    // DS14-end

    public override void Initialize()
    {
        // DS14-start: current engine uses explicit event subscriptions and query initialization.
        base.Initialize();
        SubscribeLocalEvent<PlantTraitLigneousComponent, InteractUsingEvent>(OnInteractUsing);
        _holderQuery = GetEntityQuery<PlantHolderComponent>();
        // DS14-end

        SubscribeLocalEvent<PlantTraitLigneousComponent, DoHarvestEvent>(OnDoHarvest, before: [typeof(PlantHarvestSystem)]);
    }

    private void OnInteractUsing(Entity<PlantTraitLigneousComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!_holderQuery.TryComp(ent.Owner, out var holder)) // DS14
            return;

        if (!holder.ReadyForHarvest) // DS14
            return;

        if (_plantHolder.IsDead(ent.Owner))
        {
            _popup.PopupCursor(Loc.GetString("plant-component-dead-plant-message"), args.User);
            return;
        }

        // Ligneous requires sharp tool.
        var harvestToolQuality = ent.Comp.HarvestToolQuality;
        if (harvestToolQuality.HasValue && !_tool.HasQuality(args.Used, harvestToolQuality.Value))
        {
            _popup.PopupCursor(Loc.GetString("plant-component-ligneous-cant-harvest-message"), args.User);
            return;
        }

        _plantHarvest.TryHandleHarvest(ent.Owner, args.User);
        args.Handled = true;
    }

    private void OnDoHarvest(Entity<PlantTraitLigneousComponent> ent, ref DoHarvestEvent args)
    {
        _popup.PopupCursor(Loc.GetString("plant-component-ligneous-cant-harvest-message"), args.User);
        args.Cancel();
    }
}
