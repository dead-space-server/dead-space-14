// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Chemistry.Components;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Surgery;

public sealed partial class GraftMutagenSpoil : EntityEffectBase<GraftMutagenSpoil>
{
    [DataField(required: true)]
    public string Reagent = "";

    [DataField(required: true)]
    public string Into = "";

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-graft-spoil", ("chance", Probability));
}

public sealed partial class GraftMutagenSpoilSystem : EntityEffectSystem<SolutionComponent, GraftMutagenSpoil>
{
    protected override void Effect(Entity<SolutionComponent> entity, ref EntityEffectEvent<GraftMutagenSpoil> args)
    {
        var solution = entity.Comp.Solution;
        var amount = solution.GetTotalPrototypeQuantity(args.Effect.Reagent);
        if (amount <= 0)
            return;

        solution.RemoveReagent(args.Effect.Reagent, amount);
        solution.AddReagent(args.Effect.Into, amount);
        Dirty(entity);
    }
}
