// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Ranged.Upgrades;
using Content.Shared.Weapons.Ranged.Upgrades.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Server.DeadSpace.Weapons.Ranged;

/// <summary>
/// The modkits of a proto-kinetic gun come out again, like a crusher's trophies: one "Eject" verb per modkit puts it
/// into the user's hands.
/// </summary>
public sealed class GunUpgradeRemovalSystem : EntitySystem
{
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly GunUpgradeSystem _upgrades = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<UpgradeableGunComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<UpgradeableGunComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        foreach (var upgrade in _upgrades.GetCurrentUpgrades(ent))
        {
            var uid = upgrade.Owner;
            args.Verbs.Add(new Verb
            {
                Text = Name(uid),
                Category = VerbCategory.Eject,
                Act = () => Remove(ent, uid, user),
            });
        }
    }

    private void Remove(Entity<UpgradeableGunComponent> gun, EntityUid upgrade, EntityUid user)
    {
        if (!_container.TryGetContainer(gun, gun.Comp.UpgradesContainerId, out var container) ||
            !container.Contains(upgrade) ||
            !_container.Remove(upgrade, container))
        {
            return;
        }

        _hands.PickupOrDrop(user, upgrade);
        _gun.RefreshModifiers(gun.Owner);
        _audio.PlayPvs(gun.Comp.InsertSound, gun);
        _popup.PopupEntity(Loc.GetString("gun-upgrade-popup-remove", ("upgrade", upgrade), ("gun", gun.Owner)), user, user);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(user):player} removed gun upgrade {ToPrettyString(upgrade)} from {ToPrettyString(gun.Owner)}.");
    }
}
