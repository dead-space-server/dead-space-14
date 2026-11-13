// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Necromorphs.InfectionDead.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.Effects;

public sealed partial class NecromorphMutagen : EntityEffectBase<NecromorphMutagen>
{
    [DataField]
    public bool IsAnimal = false;

    [DataField(required: true)]
    public string? NecroPrototype { get; set; } = null;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-necromorph-mutagen", ("chance", Probability));
}
