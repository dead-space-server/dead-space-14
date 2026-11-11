// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Body.Limbs;

public sealed partial class ChiyuriMend : EntityEffectBase<ChiyuriMend>
{
    [DataField]
    public float Dose = 15f;

    [DataField]
    public float Rate = 0.5f;

    [DataField]
    public float Regrow = 0.08f;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-chiyuri", ("dose", Dose));
}
