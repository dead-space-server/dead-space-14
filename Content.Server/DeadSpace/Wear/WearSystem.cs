// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Wear.Components;
using Content.Server.Destructible;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Robust.Server.Audio;

namespace Content.Server.DeadSpace.Wear;

public sealed partial class WearSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private DestructibleSystem _destructible = default!;
    [Dependency] private DamageableSystem _damage = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WearComponent, ExaminedEvent>(OnExamine);
    }

    private void OnExamine(EntityUid uid, WearComponent component, ExaminedEvent args)
    {
        var remaining = GetRemainingDurability(uid);

        args.PushMarkup(Loc.GetString("wear-exm-info", ("points", Math.Round(remaining.Float()).ToString())));
    }

    public void AddWear(EntityUid uid, DamageSpecifier damage, WearComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        _damage.TryChangeDamage(uid,
                damage,
                origin: uid,
                ignoreResistances: false,
                interruptsDoAfters: false);

        if (component.Sound != null)
            _audio.PlayPvs(component.Sound, Transform(uid).Coordinates);

    }

    public FixedPoint2 GetRemainingDurability(EntityUid uid, DamageableComponent? damageable = null, DestructibleComponent? destructible = null)
    {
        if (!Resolve(uid, ref damageable))
            return FixedPoint2.Zero;

        if (!Resolve(uid, ref destructible))
            return FixedPoint2.Zero;

        var destroyedAt = _destructible.DestroyedAt(uid, destructible);

        return FixedPoint2.Max(destroyedAt - damageable.TotalDamage, FixedPoint2.Zero);
    }

}