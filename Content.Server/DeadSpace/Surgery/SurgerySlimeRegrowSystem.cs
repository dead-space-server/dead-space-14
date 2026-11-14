// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.Surgery;

public sealed class SurgerySlimeRegrowSystem : EntitySystem
{
    private static readonly LimbZone[] Order =
    {
        LimbZone.Head,
        LimbZone.ArmLeft,
        LimbZone.ArmRight,
        LimbZone.HandLeft,
        LimbZone.HandRight,
        LimbZone.LegLeft,
        LimbZone.LegRight,
        LimbZone.FootLeft,
        LimbZone.FootRight,
    };

    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly LimbWoundSystem _limbs = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgerySlimeRegrowComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<SurgerySlimeRegrowComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SurgerySlimeRegrowComponent, SurgerySlimeRegrowEvent>(OnRegrow);
    }

    private void OnInit(Entity<SurgerySlimeRegrowComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.ActionEntity, ent.Comp.Action);
    }

    private void OnShutdown(Entity<SurgerySlimeRegrowComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);
    }

    private void OnRegrow(Entity<SurgerySlimeRegrowComponent> ent, ref SurgerySlimeRegrowEvent args)
    {
        if (args.Handled || !_mobs.IsAlive(ent.Owner) || !TryComp<LimbBodyComponent>(ent.Owner, out var limbs) || !limbs.Soft)
            return;

        args.Handled = true;
        LimbZone? target = null;
        foreach (var zone in Order)
        {
            var index = LimbWoundRules.Index(zone);
            if ((uint) index >= (uint) limbs.Zones.Count || !limbs.Zones[index].Severed)
                continue;

            target = zone;
            break;
        }

        if (target == null)
        {
            _popup.PopupEntity(Loc.GetString("surgery-slime-regrow-none"), ent.Owner, ent.Owner);
            return;
        }

        var fraction = _random.NextFloat(0.05f, 0.1f);
        if (!_limbs.TryRegrowZone(ent.Owner, target.Value, fraction))
        {
            _popup.PopupEntity(Loc.GetString("surgery-slime-regrow-fail"), ent.Owner, ent.Owner);
            return;
        }

        var hurt = new DamageSpecifier();
        hurt.DamageDict["Blunt"] = LimbWoundRules.HealthFor(target.Value, CompOrNull<LimbBodyComponent>(ent.Owner)) * 0.15f;
        _damage.TryChangeDamage(ent.Owner, hurt, true, false);
        _popup.PopupEntity(Loc.GetString("surgery-slime-regrow-ok", ("zone", Loc.GetString($"health-analyzer-zone-{(byte) target.Value}"))), ent.Owner, ent.Owner);
    }
}
