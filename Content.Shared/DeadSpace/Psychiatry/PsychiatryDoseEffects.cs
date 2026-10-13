// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Psychiatry;

public sealed partial class PsychiatryClarityDose : EntityEffectBase<PsychiatryClarityDose>
{
    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-psychiatry-clarity");
}

public sealed partial class PsychiatryPsychogenDose : EntityEffectBase<PsychiatryPsychogenDose>
{
    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-psychiatry-psychogen");
}

public sealed partial class PsychiatryInhale : EntityEffectBase<PsychiatryInhale>
{
    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-psychiatry-inhale");
}
