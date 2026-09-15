using Content.Shared.Botany.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.Botany.Traits.Components;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.EntityEffects.Effects.Botany.PlantAttributes;

/// <summary>
/// Entity effect that adds or removes a plant trait.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class PlantChangeTraitsEntityEffectSystem : EntityEffectSystem<PlantComponent, PlantChangeTraits>
{
    // DS14-start: current engine uses readonly IoC fields.
    [Dependency] private readonly PlantHolderSystem _plantHolder = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    // DS14-end

    protected override void Effect(Entity<PlantComponent> entity, ref EntityEffectEvent<PlantChangeTraits> args)
    {
        if (_plantHolder.IsDead(entity.Owner))
            return;

        var traitType = _componentFactory.GetComponent(args.Effect.Trait);
        if (traitType is not PlantTraitsComponent)
        {
            Log.Error(
                $"Component '{traitType}' (type: {traitType.GetType().Name}) is not a descendant of {nameof(PlantTraitsComponent)}.");
            return;
        }

        // DS14-start - random mutations can toggle traits without changing the default idempotent add behavior.
        if (args.Effect.Remove)
            RemCompDeferred(entity.Owner, traitType.GetType());
        else if (args.Effect.Toggle && HasComp(entity.Owner, traitType.GetType()))
            RemCompDeferred(entity.Owner, traitType.GetType());
        else if (!HasComp(entity.Owner, traitType.GetType()))
            AddComp(entity.Owner, traitType);
        // DS14-end
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class PlantChangeTraits : EntityEffectBase<PlantChangeTraits>
{
    /// <summary>
    /// Name of a <see cref="PlantTraitsComponent"/> type.
    /// </summary>
    [DataField(required: true, customTypeSerializer: typeof(ComponentNameSerializer))]
    public string Trait;

    /// <summary>
    /// If true, the trait is always removed and takes priority over <see cref="Toggle"/>.
    /// Otherwise the trait is added, or toggled when <see cref="Toggle"/> is true.
    /// </summary>
    [DataField]
    public bool Remove;

    // DS14-start
    /// <summary>
    /// If true, an existing trait is removed and a missing trait is added.
    /// </summary>
    [DataField]
    public bool Toggle;
    // DS14-end
}
