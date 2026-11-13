// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Virus.Systems;
using Content.Shared.DeadSpace.Virus.Components;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects;

namespace Content.Server.EntityEffects.Effects.DeadSpace;

/// <summary>
/// Applies medicine damage to a virus on this entity, scaled by the effect multiplier.
/// </summary>
/// <remarks>
/// The Medicine field on the DamageDisease effect should be set to the reagent prototype ID
/// so that per-medicine resistance tracking works correctly.
/// </remarks>
public sealed partial class DamageDiseaseEntityEffectSystem : EntityEffectSystem<VirusComponent, DamageDisease>
{
    [Dependency] private VirusSystem _virus = default!;

    protected override void Effect(Entity<VirusComponent> entity, ref EntityEffectEvent<DamageDisease> args)
    {
        var scale = args.Scale;

        if (scale <= 0f)
            return;

        if (args.Effect.Medicine == null)
        {
            Log.Warning("DamageDisease effect is missing the Medicine field for resistance tracking.");
            return;
        }

        var finalDamage = args.Effect.BaseDamage * scale;
        var resistanceIncrease = args.Effect.ResistanceIncrease * scale;

        _virus.ApplyMedicineDamage(
            (entity, entity.Comp),
            args.Effect.Medicine.Value,
            finalDamage,
            resistanceIncrease
        );
    }
}
