using Content.Shared.Botany.Events;
using Content.Shared.Botany.Traits.Components;
using Robust.Shared.Audio.Systems;

namespace Content.Shared.Botany.Traits.Systems;

/// <inheritdoc cref="PlantTraitScreamComponent"/>
public sealed partial class PlantTraitScreamSystem : EntitySystem
{
    // DS14-start: current engine uses explicit event subscriptions.
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlantTraitScreamComponent, AfterDoHarvestEvent>(OnAfterDoHarvest);
    }
    // DS14-end

    // DS14-start: current engine uses readonly IoC fields.
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    // DS14-end

    private void OnAfterDoHarvest(Entity<PlantTraitScreamComponent> ent, ref AfterDoHarvestEvent args)
    {
        _audio.PlayPredicted(ent.Comp.ScreamSound, ent, args.User);
    }
}
