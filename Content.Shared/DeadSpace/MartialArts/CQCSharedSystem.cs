// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Shared.DeadSpace.MartialArts.CQC.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Interaction.Events;
using Robust.Shared.Timing;

namespace Content.Shared.DeadSpace.MartialArts.CQC;

public abstract partial class CQCSharedSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CQCCantUseWeaponComponent, ShotAttemptedEvent>(OnShotAttempt);
        SubscribeLocalEvent<CQCCantUseWeaponComponent, AttackAttemptEvent>(OnAttackAttempt);
    }

    private void OnShotAttempt(Entity<CQCCantUseWeaponComponent> ent, ref ShotAttemptedEvent args)
    {
        ShowPopup(ent);
        args.Cancel();
    }

    private void OnAttackAttempt(Entity<CQCCantUseWeaponComponent> ent, ref AttackAttemptEvent args)
    {
        if (args.Weapon == null || args.Weapon.Value.Owner == ent.Owner)
            return;

        ShowPopup(ent);
        args.Cancel();
    }

    private void ShowPopup(Entity<CQCCantUseWeaponComponent> user)
    {
        if (_timing.CurTime < user.Comp.NextPopupTime)
            return;

        _popup.PopupClient(Loc.GetString("gun-disabled"), user, user);
        user.Comp.NextPopupTime = _timing.CurTime + user.Comp.PopupCooldown;
    }
}
