// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Wizard.Runes;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Slippery;
using Content.Shared.Stunnable;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.Wizard.Runes;

public sealed partial class RuneCasterSystem
{
    private readonly HashSet<EntityUid> _slipped = new();
    private readonly Dictionary<EntityUid, TimeSpan> _slipAttempts = new();

    private void InitializeSlip()
    {
        SubscribeLocalEvent<RuneCasterComponent, SlipAttemptEvent>(OnSlipAttempt);
        SubscribeLocalEvent<RuneCasterComponent, KnockedDownEvent>(OnKnockedDown);
    }

    private void OnSlipAttempt(Entity<RuneCasterComponent> ent, ref SlipAttemptEvent args)
    {
        if (!args.NoSlip)
            _slipAttempts[ent] = Timing.CurTime;
    }

    private void OnKnockedDown(Entity<RuneCasterComponent> ent, ref KnockedDownEvent args)
    {
        if (_slipAttempts.Remove(ent, out var at) && at == Timing.CurTime)
            _slipped.Add(ent);
    }

    private void UpdateSlip()
    {
        _slipAttempts.Clear();
        if (_slipped.Count == 0)
            return;

        foreach (var uid in _slipped)
        {
            if (TryComp<RuneCasterComponent>(uid, out var caster))
                ResolveSlip((uid, caster));
        }

        _slipped.Clear();
    }

    private void ResolveSlip(Entity<RuneCasterComponent> caster)
    {
        if (!TryComp<RuneSigilComponent>(caster, out var sigil) || sigil.State != RuneSigilState.Drawing || sigil.Chain.Count == 0)
            return;

        var chain = new List<string>(sigil.Chain);
        if (!ValidChain(caster.Comp, chain) || !TryCompose(chain, out var composition) || composition.Spell == null)
        {
            StumbleSigil(caster);
            _popup.PopupEntity(Loc.GetString("rune-slip-lost"), caster, caster, PopupType.SmallCaution);
            return;
        }

        _popup.PopupEntity(Loc.GetString("rune-slip-cast"), caster, caster, PopupType.MediumCaution);
        if (!TryCast(caster, composition, SlipTarget(caster, composition), false))
        {
            StumbleSigil(caster);
            return;
        }

        if (TryComp<ActorComponent>(caster, out var actor))
            RaiseNetworkEvent(new RuneChainResetEvent(), actor.PlayerSession);
    }

    private EntityCoordinates SlipTarget(EntityUid caster, RuneComposition composition)
    {
        var spell = composition.Spell!;
        var xform = Transform(caster);
        if (spell.Targeting == RuneTargeting.Self)
            return xform.Coordinates;

        var from = _transform.GetMapCoordinates(caster, xform);
        var range = MathF.Max(1f, spell.Range * composition.RangeScale);

        if (spell.Targeting is RuneTargeting.Entity or RuneTargeting.EntityOrSelf)
        {
            var mobs = new List<EntityUid>();
            foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(from, range))
            {
                if (mob.Owner != caster)
                    mobs.Add(mob);
            }

            if (mobs.Count > 0)
                return Transform(_random.Pick(mobs)).Coordinates;

            if (spell.Targeting == RuneTargeting.EntityOrSelf)
                return xform.Coordinates;
        }

        var direction = TryComp<PhysicsComponent>(caster, out var physics) && physics.LinearVelocity.LengthSquared() > 0.01f
            ? Vector2.Normalize(physics.LinearVelocity)
            : _transform.GetWorldRotation(xform).ToWorldVec();
        var point = from.Position + direction * _random.NextFloat(1f, range);
        return _transform.ToCoordinates(new MapCoordinates(point, from.MapId));
    }
}
