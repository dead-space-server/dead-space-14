// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.AntiAlcohol;

/// <summary>
///     Простая метка для метаболизма спирта: когда эффект срабатывает,
///     серверный антиалкогольный имплант получает событие с <see cref="EntityEffectReagentArgs"/>
/// </summary>
public sealed partial class AntiAlcoholImplantEffect : EventEntityEffect<AntiAlcoholImplantEffect>
{
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => null;
}
