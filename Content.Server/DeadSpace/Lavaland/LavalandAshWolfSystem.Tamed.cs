// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.Actions;
using Content.Shared.DeadSpace.Lavaland.AshWolf;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// The tamed Ash Wolf, a borg now: whoever is put in it has its rush, leap, retreat and pounce as actions (the pounce
/// on a long cooldown). They work as the boss's own do, against anyone alive.
/// </summary>
public sealed partial class LavalandAshWolfSystem
{
    private void InitializeTamed()
    {
        SubscribeLocalEvent<LavalandAshWolfComponent, LavalandAshWolfRushActionEvent>(OnRushAction);
        SubscribeLocalEvent<LavalandAshWolfComponent, LavalandAshWolfLeapActionEvent>(OnLeapAction);
        SubscribeLocalEvent<LavalandAshWolfComponent, LavalandAshWolfRetreatActionEvent>(OnRetreatAction);
        SubscribeLocalEvent<LavalandAshWolfComponent, LavalandAshWolfPounceActionEvent>(OnPounceAction);
    }

    private void OnRushAction(Entity<LavalandAshWolfComponent> ent, ref LavalandAshWolfRushActionEvent args)
    {
        TryTamedAttack(ent, args, LavalandAshWolfAttack.Rush);
    }

    private void OnLeapAction(Entity<LavalandAshWolfComponent> ent, ref LavalandAshWolfLeapActionEvent args)
    {
        TryTamedAttack(ent, args, LavalandAshWolfAttack.Leap);
    }

    private void OnRetreatAction(Entity<LavalandAshWolfComponent> ent, ref LavalandAshWolfRetreatActionEvent args)
    {
        TryTamedAttack(ent, args, LavalandAshWolfAttack.Retreat);
    }

    private void OnPounceAction(Entity<LavalandAshWolfComponent> ent, ref LavalandAshWolfPounceActionEvent args)
    {
        TryTamedAttack(ent, args, LavalandAshWolfAttack.Pounce);
    }

    private void TryTamedAttack(Entity<LavalandAshWolfComponent> ent, WorldTargetActionEvent args, LavalandAshWolfAttack attack)
    {
        if (args.Handled || !ent.Comp.Tamed || ent.Comp.Attack != LavalandAshWolfAttack.None || !_mobState.IsAlive(ent))
            return;

        var target = _transform.ToMapCoordinates(args.Target);
        if (target.MapId != Transform(ent).MapID)
            return;

        ent.Comp.AimPoint = target.Position;
        BeginAttack(ent, attack, _timing.CurTime);
        args.Handled = true;
    }

    private void UpdateTamed(Entity<LavalandAshWolfComponent> ent, TimeSpan now)
    {
        var comp = ent.Comp;
        if (comp.Attack == LavalandAshWolfAttack.None)
            return;

        // Knocked out of it (critical, dead): the attack is dropped.
        if (!_mobState.IsAlive(ent))
        {
            EndAttack(ent, now);
            return;
        }

        UpdateSlide(ent, now);
        UpdateGrind(ent, now);
        UpdateAttack(ent, new List<EntityUid>(), now);
    }
}
