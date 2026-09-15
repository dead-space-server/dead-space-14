using Content.Shared.CombatMode;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.Effects;

/// <summary>
/// Disarms the affected entity.
/// </summary>
public sealed partial class DisarmEntityEffectSystem : EntityEffectSystem<MetaDataComponent, Disarm>
{
    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<Disarm> args)
    {
        var disarm = new DisarmedEvent(entity.Owner, entity.Owner, 0f);
        RaiseLocalEvent(entity.Owner, ref disarm);
    }
}

public sealed partial class Disarm : EntityEffectBase<Disarm>
{
    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-disarm", ("chance", Probability));
}
