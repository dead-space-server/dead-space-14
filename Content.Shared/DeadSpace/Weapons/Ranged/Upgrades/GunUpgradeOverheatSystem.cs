// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Timing;

namespace Content.Shared.DeadSpace.Weapons.Ranged.Upgrades;

/// <summary>
/// Overheating from an upgrade (<see cref="GunUpgradeOverheatComponent"/>). The gun's shots and shot attempts reach
/// the upgrade through GunUpgradeSystem's relay.
/// </summary>
public sealed class GunUpgradeOverheatSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunUpgradeOverheatComponent, GunShotEvent>(OnShot);
        SubscribeLocalEvent<GunUpgradeOverheatComponent, AttemptShootEvent>(OnAttemptShoot);
    }

    private void OnAttemptShoot(Entity<GunUpgradeOverheatComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled)
            return;

        var now = _timing.CurTime;
        if (now >= ent.Comp.LockedUntil)
            return;

        args.Cancelled = true;
        if (ent.Comp.LockedPopup is { } popup)
            args.Message = Loc.GetString(popup, ("seconds", (int) Math.Ceiling((ent.Comp.LockedUntil - now).TotalSeconds)));
    }

    private void OnShot(Entity<GunUpgradeOverheatComponent> ent, ref GunShotEvent args)
    {
        var now = _timing.CurTime;
        if (now - ent.Comp.LastShot > ent.Comp.ResetDelay)
            ent.Comp.Count = 0;

        ent.Comp.Count++;
        ent.Comp.LastShot = now;

        if (ent.Comp.Count >= ent.Comp.Shots)
        {
            ent.Comp.Count = 0;
            ent.Comp.LockedUntil = now + ent.Comp.Lockout;
            if (ent.Comp.OverheatPopup is { } popup)
                _popup.PopupClient(Loc.GetString(popup), args.User, args.User, PopupType.SmallCaution);
        }

        Dirty(ent);
    }
}
