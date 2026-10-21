using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.Weapons.Ranged.Systems;

public sealed class RechargeBasicEntityAmmoSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly Content.Shared.Weapons.Ranged.Upgrades.GunUpgradeSystem _gunUpgrade = default!; // DS14

    private EntityQuery<EmpDisabledComponent> _empDisabledQuery;

    public override void Initialize()
    {
        base.Initialize();

        _empDisabledQuery = GetEntityQuery<EmpDisabledComponent>();

        SubscribeLocalEvent<RechargeBasicEntityAmmoComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<RechargeBasicEntityAmmoComponent, ExaminedEvent>(OnExamined);
        // DS14-start EMP-disables-basic-entity-ammo
        SubscribeLocalEvent<EmpDisableBasicEntityAmmoComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<EmpDisableBasicEntityAmmoComponent, ShotAttemptedEvent>(OnShotAttempted);
        // DS14-end
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<RechargeBasicEntityAmmoComponent, BasicEntityAmmoProviderComponent>();

        while (query.MoveNext(out var uid, out var recharge, out var ammo))
        {
            // DS14-start EMP-disables-basic-entity-ammo
            if (_empDisabledQuery.HasComp(uid))
                continue;
            // DS14-end

            if (ammo.Count is null || ammo.Count == ammo.Capacity || recharge.NextCharge == null)
                continue;

            if (recharge.NextCharge > _timing.CurTime)
                continue;

            if (_gun.UpdateBasicEntityAmmoCount((uid, ammo), ammo.Count.Value + 1))
            {
                // We don't predict this because occasionally on client it may not play.
                // PlayPredicted will still be predicted on the client.
                if (_netManager.IsServer)
                    _audio.PlayPvs(recharge.RechargeSound, uid);
            }

            if (ammo.Count == ammo.Capacity)
            {
                recharge.NextCharge = null;
                Dirty(uid, recharge);
                continue;
            }

            recharge.NextCharge = recharge.NextCharge.Value + TimeSpan.FromSeconds(GetCooldown(uid, recharge)); // DS14
            Dirty(uid, recharge);
        }
    }

    // DS14-start
    /// <summary>
    /// The recharge, quickened by whatever speeds up the gun's fire (a fire rate upgrade): on a gun that has to wait
    /// for its charges (a kinetic shotgun or repeater) the faster fire alone changed nothing.
    /// </summary>
    private float GetCooldown(EntityUid uid, RechargeBasicEntityAmmoComponent recharge)
    {
        if (!TryComp<GunComponent>(uid, out var gun) || gun.FireRate <= 0f || gun.FireRateModified <= 0f)
            return recharge.RechargeCooldown;

        // Upgrades kept off the fire rate itself (the repeater) still count here.
        var boost = gun.FireRateModified / gun.FireRate * _gunUpgrade.GetFireRateUpgradeBoost(uid);
        return boost > 1f ? recharge.RechargeCooldown / boost : recharge.RechargeCooldown;
    }
    // DS14-end

    private void OnInit(Entity<RechargeBasicEntityAmmoComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextCharge = _timing.CurTime;
        Dirty(ent);
    }

    private void OnExamined(Entity<RechargeBasicEntityAmmoComponent> ent, ref ExaminedEvent args)
    {
        if (!ent.Comp.ShowExamineText)
            return;

        if (!TryComp<BasicEntityAmmoProviderComponent>(ent, out var ammo)
            || ammo.Count == ammo.Capacity ||
            ent.Comp.NextCharge == null)
        {
            args.PushMarkup(Loc.GetString("recharge-basic-entity-ammo-full"));
            return;
        }

        var timeLeft = ent.Comp.NextCharge + _metadata.GetPauseTime(ent) - _timing.CurTime;
        args.PushMarkup(Loc.GetString("recharge-basic-entity-ammo-can-recharge", ("seconds", Math.Round(timeLeft.Value.TotalSeconds, 1))));
    }

    public void Reset(Entity<RechargeBasicEntityAmmoComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (ent.Comp.NextCharge == null || ent.Comp.NextCharge < _timing.CurTime)
        {
            ent.Comp.NextCharge = _timing.CurTime + TimeSpan.FromSeconds(GetCooldown(ent, ent.Comp)); // DS14
            Dirty(ent);
        }
    }

    // DS14-start EMP-disables-basic-entity-ammo
    private void OnEmpPulse(Entity<EmpDisableBasicEntityAmmoComponent> ent, ref EmpPulseEvent args)
    {
        args.Affected = true;
        args.Disabled = true;

        if (TryComp<BasicEntityAmmoProviderComponent>(ent.Owner, out var ammo))
            _gun.UpdateBasicEntityAmmoCount((ent.Owner, ammo), 0);

        if (!TryComp<RechargeBasicEntityAmmoComponent>(ent.Owner, out var recharge))
            return;

        recharge.NextCharge = _timing.CurTime + args.Duration;
        Dirty(ent.Owner, recharge);
    }

    private void OnShotAttempted(Entity<EmpDisableBasicEntityAmmoComponent> ent, ref ShotAttemptedEvent args)
    {
        if (_empDisabledQuery.HasComp(ent.Owner))
            args.Cancel();
    }
    // DS14-end
}
