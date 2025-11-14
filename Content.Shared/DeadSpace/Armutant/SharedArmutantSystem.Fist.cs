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

        _speed.ChangeBaseSpeed(ent, args.BuffSpeedWalk, args.BuffSpeedSprint, 20f);
        _popup.PopupEntity(Loc.GetString("speed-buff-start"), ent, ent);

        Timer.Spawn(TimeSpan.FromSeconds(args.TimeRecovery), () =>
        {
            _speed.ChangeBaseSpeed(ent, 2.5f, 4.5f, 20f);
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

        if (TryComp<CuffableComponent>(ent, out var cuffs) && cuffs.Container.ContainedEntities.Count > 0)
        {
            var ev = new UnCuffableArmEvent(ent);
            RaiseLocalEvent(ent, ev);

            var effect = Spawn(args.SelfEffect, Transform(ent).Coordinates);
            _transform.SetParent(effect, ent);
        }

        var reagents = new List<(string, FixedPoint2)>()
        {
            (args.Reagent, args.AmountReagent)
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

        var ev = new BeamActiveVoidHold(args.HandEffect, args.Target);
        RaiseLocalEvent(ent, ev);

        _stun.TryUpdateParalyzeDuration(args.Target, TimeSpan.FromSeconds(args.StunTime));

        _audio.PlayPvs(args.SoundEffect, ent);

        SpawnAttachedTo(args.TargetEffect, args.Target.ToCoordinates());

        args.Handled = true;
    }
}
