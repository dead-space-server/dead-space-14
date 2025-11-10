using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.AntiAlcohol;

/// <summary>
///     Просто маркер 
///     <see cref="EntityEffectReagentArgs"/> 
/// </summary>
public sealed partial class AntiAlcoholImplantEffect : EventEntityEffect<AntiAlcoholImplantEffect>
{
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => null;
}
