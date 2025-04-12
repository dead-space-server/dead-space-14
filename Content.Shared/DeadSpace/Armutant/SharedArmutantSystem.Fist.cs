using Content.Shared.Coordinates;
using Content.Shared.Cuffs.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.Timing;

namespace Content.Shared.DeadSpace.Armutant;

public partial class SharedArmutantSystem
{
    private void InitializeFist()
    {
        SubscribeLocalEvent<ArmutantComponent, FistBuffSpeedToggleEvent>(OnBuffSpeedAction);
        SubscribeLocalEvent<ArmutantComponent, FistMendSelfToggleEvent>(OnMendSelfAction);
        SubscribeLocalEvent<ArmutantComponent, FistStunTentacleToggleEvent>(OnFistStunTentacleAction);
    }
    private void OnBuffSpeedAction(Entity<ArmutantComponent> ent, ref FistBuffSpeedToggleEvent args)
    {
        if (!_net.IsServer)
            return;

        if (args.Handled)
            return;

        var actionEnt = args.Action.Owner;
        if (!TryComp<ArmutantFistActionComponent>(actionEnt, out var armutantActionComp))
            return;

        _speed.ChangeBaseSpeed(ent, armutantActionComp.BuffSpeedWalk, armutantActionComp.BuffSpeedSprint, 20f); // Бафф скорости, значение 20f базовое, никак не будет менять
        _popup.PopupEntity(Loc.GetString("speed-buff-start"), ent, ent);

        Timer.Spawn(TimeSpan.FromSeconds(armutantActionComp.TimeRecovery), () =>
        {
            _speed.ChangeBaseSpeed(ent, 2.5f, 4.5f, 20f); // Возвращаем в базову значению скорости
            _popup.PopupEntity(Loc.GetString("speed-buff-end"), ent, ent);
        });

        args.Handled = true;
    }
    private void OnMendSelfAction(Entity<ArmutantComponent> ent, ref FistMendSelfToggleEvent args)
    {
        if (!_net.IsServer)
            return;

        if (args.Handled)
            return;

        var actionEnt = args.Action.Owner;
        if (!TryComp<ArmutantFistActionComponent>(actionEnt, out var armutantActionComp))
            return;

        if (TryComp<CuffableComponent>(ent, out var cuffs) && cuffs.Container.ContainedEntities.Count > 0) // Если человек в наручниках, освобождаем его
        {
            var ev = new UnCuffableArmEvent(ent);
            RaiseLocalEvent(ent, ev);

            var effect = Spawn(armutantActionComp.SelfEffect, Transform(ent).Coordinates);
            _transform.SetParent(effect, ent);
        }

        var reagents = new List<(string, FixedPoint2)>() // Создаем контейнер реагента
        {
            (armutantActionComp.Reagent, armutantActionComp.AmountReagent)
        };

        if (TryInjectReagents(ent, reagents))
        {
            _popup.PopupEntity(Loc.GetString("mendself-activated"), ent, ent);
        }
        else
            return;

        args.Handled = true;
    }
    private void OnFistStunTentacleAction(Entity<ArmutantComponent> ent, ref FistStunTentacleToggleEvent args)
    {
        if (!_net.IsServer)
            return;

        if (args.Handled)
            return;

        var actionEnt = args.Action.Owner;
        if (!TryComp<ArmutantFistActionComponent>(actionEnt, out var armutantActionComp))
            return;

        var ev = new BeamActiveVoidHold(armutantActionComp.HandEffect, args.Target); // Вызываем эффект руки
        RaiseLocalEvent(ent, ev);

        _stun.TryParalyze(args.Target, TimeSpan.FromSeconds(armutantActionComp.StunTime), true); // Парализуем цель

        _audio.PlayPvs(armutantActionComp.SoundEffect, ent);

        SpawnAttachedTo(armutantActionComp.TargetEffect, args.Target.ToCoordinates());

        args.Handled = true;
    }
}
