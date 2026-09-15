// SPDX-FileCopyrightText: 2026 Kofeecheks
// SPDX-License-Identifier: LicenseRef-Kofeecheks

using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace._Soyuz.EntityEffects;

/// <summary>
/// Adjusts stamina damage directly. Positive values recover stamina damage.
/// </summary>
public sealed partial class AdjustStaminaDamageEntityEffectSystem : EntityEffectSystem<StaminaComponent, AdjustStaminaDamage>
{
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;

    protected override void Effect(Entity<StaminaComponent> entity, ref EntityEffectEvent<AdjustStaminaDamage> args)
    {
        var amount = args.Effect.Amount * args.Scale;
        if (amount == 0f)
            return;

        _stamina.TakeStaminaDamage(entity.Owner, -amount, entity.Comp, visual: false);
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class AdjustStaminaDamage : EntityEffectBase<AdjustStaminaDamage>
{
    [DataField]
    public float Amount = 0.5f;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => null;
}
