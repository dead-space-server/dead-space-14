// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.CCVar;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Configuration;

namespace Content.Shared.DeadSpace.Body.Limbs;

public sealed class PlayerStubArmorSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private float _coefficient = 0.7f;

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_cfg, CCVars.LimbStubArmor, value => _coefficient = value, true);
    }

    public void Apply(DamageModifyEvent args)
    {
        if (_coefficient >= 1f)
            return;

        var scaled = new DamageSpecifier(args.Damage);
        foreach (var (type, value) in args.Damage.DamageDict)
        {
            if (value > 0)
                scaled.DamageDict[type] = value * _coefficient;
        }

        args.Damage = scaled;
    }

    public FixedPoint2 BeforeStub(EntityUid uid, FixedPoint2 total)
    {
        if (!HasComp<LimbBodyComponent>(uid) || _coefficient <= 0f)
            return total;

        return total / _coefficient;
    }
}
