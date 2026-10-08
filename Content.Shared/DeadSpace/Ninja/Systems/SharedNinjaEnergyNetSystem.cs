// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using Content.Shared.Ninja.Systems;

namespace Content.Shared.DeadSpace.Ninja.Systems;

public sealed class SharedNinjaEnergyNetSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedSpaceNinjaSystem _ninja = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaEnergyNetGunComponent, AfterInteractEvent>(OnAfterInteractUsing);

        SubscribeLocalEvent<NinjaEnergyNetComponent, UnstrapAttemptEvent>(OnUnstrapAttemptEvent);
    }

    private void OnAfterInteractUsing(EntityUid uid, NinjaEnergyNetGunComponent component, ref AfterInteractEvent args)
    {
        if (component.RechargeTime > _timing.CurTime)
        {
            _popup.PopupClient(Loc.GetString("ninja-no-power-1"), args.User, args.User);
            return;
        }

        if (args.Target == null)
            return;

        var target = args.Target.Value;
        if (!IsValidTarget(args.User, target))
            return;

        if (!_interaction.InRangeAndAccessible(args.User, target, component.Range))
            return;

        if (_net.IsServer)
        {
            if (!_ninja.TryUseCharge(args.User, component.Charge))
            {
                _popup.PopupClient(Loc.GetString("ninja-no-power"), args.User, args.User);
                return;
            }
        }
        else if (!_ninja.HasCharge(args.User, component.Charge))
        {
            _popup.PopupClient(Loc.GetString("ninja-no-power"), args.User, args.User);
            return;
        }

        args.Handled = true;

        _ninja.RevealNinja(args.User);

        component.RechargeTime = _timing.CurTime + component.Cooldown;
        Dirty(uid, component);

        var ninjaNetEnt = PredictedSpawnAtPosition(component.NetProto, Transform(target).Coordinates);

        if (_net.IsServer)
            _audio.PlayPvs(component.FireSound, ninjaNetEnt);

        var netComp = EnsureComp<NinjaEnergyNetComponent>(ninjaNetEnt);
        netComp.BeamDeleteTime = _timing.CurTime + netComp.BeamDuration;
        Dirty(ninjaNetEnt, netComp);

        var visuals = EnsureComp<JointVisualsComponent>(ninjaNetEnt);
        visuals.Sprite = component.NetBeamSprite;
        visuals.Target = args.User;
        Dirty(ninjaNetEnt, visuals);

        if (_net.IsServer)
            _buckle.TryBuckle(target, args.User, ninjaNetEnt);
    }

    private void OnUnstrapAttemptEvent(Entity<NinjaEnergyNetComponent> ent, ref UnstrapAttemptEvent args)
    {
        args.Cancelled = true;
    }
    private bool IsValidTarget(EntityUid user, EntityUid target)
    {
        if (user == target)
            return false;

        if (!TryComp<MobStateComponent>(target, out var mobState) || _mobState.IsDead(target, mobState))
            return false;

        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        var query = EntityQueryEnumerator<NinjaEnergyNetComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (HasComp<JointVisualsComponent>(uid))
            {
                if (comp.BeamDeleteTime < _timing.CurTime)
                {
                    RemComp<JointVisualsComponent>(uid);
                }
            }
        }
    }
}
