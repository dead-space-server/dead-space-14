// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Medical;
using Content.Shared.DeadSpace.AntiAlcohol;
using Content.Shared.EntityEffects;
using Content.Shared.Damage.Systems;

namespace Content.Server.DeadSpace.AntiAlcohol;

public sealed partial class AntiAlcoholSystem : EntityEffectSystem<AntiAlcoholWatcherComponent, AntiAlcoholImplantEffect>
{
    [Dependency] private VomitSystem _vomit = default!;
    [Dependency] private DamageableSystem _damageableSystem = default!;

    protected override void Effect(Entity<AntiAlcoholWatcherComponent> entity, ref EntityEffectEvent<AntiAlcoholImplantEffect> args)
    {
        var target = entity.Owner;
        _vomit.Vomit(target);

        var finalDamage = entity.Comp.Damage * args.Scale;
        _damageableSystem.TryChangeDamage(target, finalDamage);
    }
}
