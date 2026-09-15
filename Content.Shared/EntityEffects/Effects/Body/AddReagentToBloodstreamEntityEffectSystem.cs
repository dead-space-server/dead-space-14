using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.Effects.Body;

/// <summary>
/// Adds a reagent directly to the target's bloodstream. Quantity is modified by scale.
/// </summary>
public sealed partial class AddReagentToBloodstreamEntityEffectSystem
    : EntityEffectSystem<BloodstreamComponent, AddReagentToBloodstream>
{
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly ReactiveSystem _reactive = default!;

    protected override void Effect(Entity<BloodstreamComponent> entity, ref EntityEffectEvent<AddReagentToBloodstream> args)
    {
        var solution = new Content.Shared.Chemistry.Components.Solution();
        solution.AddReagent(args.Effect.Reagent, args.Effect.Quantity * args.Scale);

        _bloodstream.TryAddToBloodstream(entity.AsNullable(), solution);
        _reactive.DoEntityReaction(entity, solution, ReactionMethod.Injection);
    }
}

public sealed partial class AddReagentToBloodstream : EntityEffectBase<AddReagentToBloodstream>
{
    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent;

    [DataField]
    public FixedPoint2 Quantity = 1;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return prototype.Resolve(Reagent, out var reagent)
            ? Loc.GetString(
                "entity-effect-guidebook-add-reagent-to-bloodstream",
                ("chance", Probability),
                ("reagent", reagent.LocalizedName),
                ("quantity", Quantity))
            : null;
    }
}
