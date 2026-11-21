using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;

namespace Content.Shared.Damage.Systems;

/// <summary>
/// Не даёт лечить сущности с <see cref="HealingImmuneComponent"/>: вырезает отрицательный урон
/// (лечение) из изменения урона, оставляя только положительный.
/// </summary>
public sealed partial class HealingImmuneSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HealingImmuneComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
    }

    private void OnBeforeDamageChanged(Entity<HealingImmuneComponent> ent, ref BeforeDamageChangedEvent args)
    {
        var hasDamage = false;
        foreach (var value in args.Damage.DamageDict.Values)
        {
            if (value <= FixedPoint2.Zero)
                continue;

            hasDamage = true;
            break;
        }

        // всё изменение было лечением - не даём ему пройти
        if (!hasDamage)
        {
            args.Cancelled = true;
            return;
        }

        var damage = new DamageSpecifier();
        foreach (var (type, value) in args.Damage.DamageDict)
        {
            if (value > FixedPoint2.Zero)
                damage.DamageDict[type] = value;
        }

        args.Damage = damage;
    }
}
