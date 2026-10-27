// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Weapons.Ranged.Components;
using Content.Shared.DeadSpace.Weapons.Ranged.Events;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Timing;

namespace Content.Shared.DeadSpace.Weapons.Ranged.Systems;

/// <summary>
/// Overheating of a self-recharging gun (<see cref="GunOverheatComponent"/>): fired too often, its charges come back
/// slower.
/// </summary>
public sealed class GunOverheatSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly RechargeBasicEntityAmmoSystem _recharge = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunOverheatComponent, GunShotEvent>(OnShot);
        SubscribeLocalEvent<GunOverheatComponent, RechargeBasicEntityAmmoCooldownEvent>(OnRechargeCooldown);
        SubscribeLocalEvent<GunOverheatComponent, ExaminedEvent>(OnExamined);
    }

    public float GetHeat(GunOverheatComponent comp)
    {
        var cooled = (float) (_timing.CurTime - comp.HeatTime).TotalSeconds * comp.Cooling;
        return MathF.Max(0f, comp.Heat - MathF.Max(0f, cooled));
    }

    private float GetSlowdown(GunOverheatComponent comp)
    {
        var over = GetHeat(comp) - comp.Threshold;
        if (over <= 0f)
            return 1f;

        return Math.Clamp(1f + over * comp.SlowdownPerHeat, 1f, MathF.Max(1f, comp.MaxSlowdown));
    }

    private void OnShot(Entity<GunOverheatComponent> ent, ref GunShotEvent args)
    {
        var before = GetHeat(ent.Comp);
        ent.Comp.Heat = before + ent.Comp.HeatPerShot;
        ent.Comp.HeatTime = _timing.CurTime;
        Dirty(ent);

        // The charge this shot has just spent was set going before the heat rose: it waits the hotter time.
        _recharge.RestartCharge(ent.Owner);

        if (before <= ent.Comp.Threshold && ent.Comp.Heat > ent.Comp.Threshold && ent.Comp.OverheatPopup is { } popup)
            _popup.PopupClient(Loc.GetString(popup, ("gun", ent.Owner)), ent, args.User, PopupType.SmallCaution);
    }

    private void OnRechargeCooldown(Entity<GunOverheatComponent> ent, ref RechargeBasicEntityAmmoCooldownEvent args)
    {
        args.Cooldown *= GetSlowdown(ent.Comp);
    }

    private void OnExamined(Entity<GunOverheatComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.OverheatExamine is { } examine && GetHeat(ent.Comp) > ent.Comp.Threshold)
            args.PushMarkup(Loc.GetString(examine, ("gun", ent.Owner)));
    }
}
