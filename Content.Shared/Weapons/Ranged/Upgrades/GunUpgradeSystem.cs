using System.Linq;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Ranged.Upgrades.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Content.Shared.DeadSpace.Weapons.Ranged.Upgrades;

namespace Content.Shared.Weapons.Ranged.Upgrades;

public sealed class GunUpgradeSystem : EntitySystem
{
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly EntityWhitelistSystem _entityWhitelist = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<UpgradeableGunComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<UpgradeableGunComponent, AfterInteractUsingEvent>(OnAfterInteractUsing);
        SubscribeLocalEvent<UpgradeableGunComponent, ExaminedEvent>(OnExamine);

        SubscribeLocalEvent<UpgradeableGunComponent, GunRefreshModifiersEvent>(RelayEvent);
        SubscribeLocalEvent<UpgradeableGunComponent, GunShotEvent>(RelayEvent);

        SubscribeLocalEvent<GunUpgradeFireRateComponent, GunRefreshModifiersEvent>(OnFireRateRefresh);
        SubscribeLocalEvent<GunUpgradeSpeedComponent, GunRefreshModifiersEvent>(OnSpeedRefresh);
        // DS14: the damage goes onto every projectile of the shot (a shotgun's pellets), not the first one only.
        SubscribeLocalEvent<UpgradeableGunComponent, AmmoShotEvent>(OnAmmoShotDamage); // DS14
    }

    private void RelayEvent<T>(Entity<UpgradeableGunComponent> ent, ref T args) where T : notnull
    {
        foreach (var upgrade in GetCurrentUpgrades(ent))
        {
            // DS14-start
            if (!_entityWhitelist.CheckBoth(upgrade.Owner, blacklist: ent.Comp.Blacklist))
                continue;
            // DS14-end
            RaiseLocalEvent(upgrade, ref args);
        }
    }

    private void OnExamine(Entity<UpgradeableGunComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(UpgradeableGunComponent)))
        {
            foreach (var upgrade in GetCurrentUpgrades(ent))
            {
                args.PushMarkup(Loc.GetString(upgrade.Comp.ExamineText));
            }
        }
    }

    private void OnInit(Entity<UpgradeableGunComponent> ent, ref ComponentInit args)
    {
        _container.EnsureContainer<Container>(ent, ent.Comp.UpgradesContainerId);
    }

    private void OnAfterInteractUsing(Entity<UpgradeableGunComponent> ent, ref AfterInteractUsingEvent args)
    {
        if (args.Handled || !args.CanReach || !TryComp<GunUpgradeComponent>(args.Used, out var upgradeComponent))
            return;

        if (GetCurrentUpgrades(ent).Count >= ent.Comp.MaxUpgradeCount)
        {
            _popup.PopupPredicted(Loc.GetString("upgradeable-gun-popup-upgrade-limit"), ent, args.User);
            return;
        }

        if (_entityWhitelist.IsWhitelistFail(ent.Comp.Whitelist, args.Used))
            return;

        // DS14-start
        if (!_entityWhitelist.CheckBoth(args.Used, blacklist: ent.Comp.Blacklist))
            return;
        // DS14-end

        if (GetCurrentUpgradeTags(ent).ToHashSet().IsSupersetOf(upgradeComponent.Tags))
        {
            _popup.PopupPredicted(Loc.GetString("upgradeable-gun-popup-already-present"), ent, args.User);
            return;
        }

        _audio.PlayPredicted(ent.Comp.InsertSound, ent, args.User);
        _popup.PopupClient(Loc.GetString("gun-upgrade-popup-insert", ("upgrade", args.Used),("gun", ent.Owner)), args.User);
        args.Handled = _container.Insert(args.Used, _container.GetContainer(ent, ent.Comp.UpgradesContainerId));
        // DS14-start: refreshed after the insert - before it the new upgrade was not counted till the next one went in.
        _gun.RefreshModifiers(ent.Owner);
        // DS14-end

        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(args.User):player} inserted gun upgrade {ToPrettyString(args.Used)} into {ToPrettyString(ent.Owner)}.");
    }

    private void OnFireRateRefresh(Entity<GunUpgradeFireRateComponent> ent, ref GunRefreshModifiersEvent args)
    {
        // DS14-start: on such a gun the upgrade only quickens the recharge (see GetFireRateUpgradeBoost).
        if (HasComp<FireRateUpgradeRechargeOnlyComponent>(args.Gun))
            return;
        // DS14-end

        args.FireRate *= ent.Comp.Coefficient;
    }

    // DS14-start
    /// <summary>
    /// The fire rate upgrades of a gun that only quicken its recharge, multiplied together; 1 for any other gun.
    /// </summary>
    public float GetFireRateUpgradeBoost(EntityUid gun)
    {
        if (!HasComp<FireRateUpgradeRechargeOnlyComponent>(gun) ||
            !TryComp<UpgradeableGunComponent>(gun, out var upgradeable))
        {
            return 1f;
        }

        var boost = 1f;
        foreach (var upgrade in GetCurrentUpgrades((gun, upgradeable)))
        {
            if (_entityWhitelist.CheckBoth(upgrade.Owner, blacklist: upgradeable.Blacklist) &&
                TryComp<GunUpgradeFireRateComponent>(upgrade, out var fireRate))
            {
                boost *= fireRate.Coefficient;
            }
        }

        return boost;
    }
    // DS14-end

    private void OnSpeedRefresh(Entity<GunUpgradeSpeedComponent> ent, ref GunRefreshModifiersEvent args)
    {
        args.ProjectileSpeed *= ent.Comp.Coefficient;
    }

    // DS14-start
    /// <summary>
    /// The upgrade's damage spread over all the projectiles of the shot: the gun shot event only knows the first one
    /// of a shotgun's spread - it got the whole bonus and the other pellets nothing.
    /// </summary>
    private void OnAmmoShotDamage(EntityUid uid, UpgradeableGunComponent component, AmmoShotEvent args)
    {
        var count = args.FiredProjectiles.Count;
        if (count == 0)
            return;

        foreach (var upgrade in GetCurrentUpgrades((uid, component)))
        {
            if (!_entityWhitelist.CheckBoth(upgrade.Owner, blacklist: component.Blacklist))
                continue;

            // The range upgrade: the projectiles live longer.
            if (TryComp<GunUpgradeSpeedComponent>(upgrade, out var speed) &&
                MathF.Abs(speed.LifetimeCoefficient - 1f) > 0.001f)
            {
                foreach (var projectile in args.FiredProjectiles)
                {
                    if (TryComp<TimedDespawnComponent>(projectile, out var despawn))
                        despawn.Lifetime *= speed.LifetimeCoefficient;
                }
            }

            if (!TryComp<GunUpgradeDamageComponent>(upgrade, out var damage))
                continue;

            var share = damage.Damage * (1f / count);
            foreach (var projectile in args.FiredProjectiles)
            {
                if (TryComp<ProjectileComponent>(projectile, out var proj))
                    proj.Damage += share;
            }
        }
    }
    // DS14-end

    /// <summary>
    /// Gets the entities inside the gun's upgrade container.
    /// </summary>
    public HashSet<Entity<GunUpgradeComponent>> GetCurrentUpgrades(Entity<UpgradeableGunComponent> ent)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.UpgradesContainerId, out var container))
            return new HashSet<Entity<GunUpgradeComponent>>();

        var upgrades = new HashSet<Entity<GunUpgradeComponent>>();
        foreach (var contained in container.ContainedEntities)
        {
            if (TryComp<GunUpgradeComponent>(contained, out var upgradeComp))
                upgrades.Add((contained, upgradeComp));
        }

        return upgrades;
    }

    /// <summary>
    /// Gets the tags of the upgrades currently applied.
    /// </summary>
    public IEnumerable<ProtoId<TagPrototype>> GetCurrentUpgradeTags(Entity<UpgradeableGunComponent> ent)
    {
        foreach (var upgrade in GetCurrentUpgrades(ent))
        {
            foreach (var tag in upgrade.Comp.Tags)
            {
                yield return tag;
            }
        }
    }
}
