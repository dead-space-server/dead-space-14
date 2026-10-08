// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.EntityEffects;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Psychiatry;

public sealed partial class ClearSensoryFaultSystem : EntityEffectSystem<MetaDataComponent, ClearSensoryFault>
{
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly BlindableSystem _blind = default!;

    private static readonly EntProtoId Deaf = "StatusEffectDeaf";
    private static readonly EntProtoId Muted = "StatusEffectMuted";

    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<ClearSensoryFault> args)
    {
        _status.TryRemoveStatusEffect(entity, Deaf);
        _status.TryRemoveStatusEffect(entity, Muted);

        if (!TryComp<BlindableComponent>(entity, out var blind) || blind.EyeDamage <= 0)
            return;

        _blind.AdjustEyeDamage((entity.Owner, blind), -blind.EyeDamage);
    }
}

public sealed partial class ClearSensoryFault : EntityEffectBase<ClearSensoryFault>
{
    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => null;
}
