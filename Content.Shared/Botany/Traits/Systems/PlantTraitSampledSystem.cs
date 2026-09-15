using Content.Shared.Botany.Events;
using Content.Shared.Botany.Traits.Components;
using Content.Shared.Popups;

namespace Content.Shared.Botany.Traits.Systems;

/// <inheritdoc cref="PlantTraitSampledComponent"/>
public sealed partial class PlantTraitSampledSystem : EntitySystem
{
    // DS14-start: current engine uses explicit event subscriptions.
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlantTraitSampledComponent, PlantSampleAttemptEvent>(OnPlantSampleAttempt);
    }
    // DS14-end

    // DS14-start: current engine uses readonly IoC fields.
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    // DS14-end

    private void OnPlantSampleAttempt(Entity<PlantTraitSampledComponent> ent, ref PlantSampleAttemptEvent args)
    {
        _popup.PopupCursor(Loc.GetString("plant-sample-component-already-sampled-popup"), args.User);
        args.Cancel();
    }
}
