// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Husbandry.Core;
using Content.Shared.DeadSpace.Husbandry.Needs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Husbandry.Needs;

/// <summary>
/// Drains the needs of animals at a constant rate and hurts the ones that ran out of something.
/// Changing the needs is in <see cref="SharedAnimalNeedsSystem"/>.
/// </summary>
public sealed class AnimalNeedsSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAnimalNeedsSystem _needs = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AnimalNeedsComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextUpdate)
                continue;

            // The step is as long as the time since the last one, not the rate: a lagging server must not slow
            // the hunger down.
            var seconds = (float) (comp.UpdateRate + (now - comp.NextUpdate)).TotalSeconds;
            comp.NextUpdate = now + comp.UpdateRate;

            if (_mobState.IsDead(uid))
                continue;

            comp.Satiety.DeprivedMinutes += NeedRules.Tick(ref comp.Satiety.Value, comp.Satiety.DecayPerMinute, seconds);
            comp.Hydration.DeprivedMinutes += NeedRules.Tick(ref comp.Hydration.Value, comp.Hydration.DecayPerMinute, seconds);

            _needs.UpdateLevels((uid, comp));

            if (now >= comp.NextDamage)
            {
                comp.NextDamage = now + comp.DamageInterval;
                ApplyDeprivationDamage(uid, comp);
            }
        }
    }

    private void ApplyDeprivationDamage(EntityUid uid, AnimalNeedsComponent comp)
    {
        var total = new DamageSpecifier();
        AddDeprivation(ref total, comp.Satiety);
        AddDeprivation(ref total, comp.Hydration);

        if (!total.Empty)
            _damageable.TryChangeDamage(uid, total, ignoreResistances: true, interruptsDoAfters: false);
    }

    private static void AddDeprivation(ref DamageSpecifier total, NeedState need)
    {
        if (need.DeprivedMinutes > 0f && !need.DeprivationDamage.Empty)
            total += need.DeprivationDamage * need.DeprivedMinutes;

        need.DeprivedMinutes = 0f;
    }
}
