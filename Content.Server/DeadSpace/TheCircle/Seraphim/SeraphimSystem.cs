// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.TheCircle.Seraphim;
using Content.Shared.DoAfter;
using Content.Shared.EntityConditions.Conditions;
using Content.Shared.EntityEffects.Effects.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.TheCircle.Seraphim;

public sealed partial class SeraphimSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private LimbWoundSystem _limbs = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly SoundSpecifier BondSound =
        new SoundPathSpecifier("/Audio/_DeadSpace/Necromorfs/TheCircle/Weapon/sfx/hook-sfx/shot_hook.ogg");
    private static readonly SoundSpecifier BondBreakSound = new SoundCollectionSpecifier("splinter-blade-retract");
    private static readonly SoundSpecifier MendSound = new SoundPathSpecifier("/Audio/Effects/Changeling/devour_suck.ogg");

    private static readonly TimeSpan BondTick = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Smallest amount of a reagent at which it starts to hurt, or null when it never does.
    /// </summary>
    private readonly Dictionary<string, FixedPoint2?> _overdose = new();

    private readonly List<ReagentQuantity> _reagents = new();

    private bool _mirroring;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SeraphimMendComponent, MapInitEvent>(OnMendInit);
        SubscribeLocalEvent<SeraphimMendComponent, ComponentShutdown>(OnMendShutdown);
        SubscribeLocalEvent<SeraphimMendComponent, SeraphimMendActionEvent>(OnMendAction);
        SubscribeLocalEvent<SeraphimMendComponent, SeraphimMendDoAfterEvent>(OnMendDoAfter);

        SubscribeLocalEvent<SeraphimBondComponent, MapInitEvent>(OnBondInit);
        SubscribeLocalEvent<SeraphimBondComponent, ComponentShutdown>(OnBondShutdown);
        SubscribeLocalEvent<SeraphimBondComponent, SeraphimBondActionEvent>(OnBondAction);
        SubscribeLocalEvent<SeraphimBondComponent, DamageChangedEvent>(OnSeraphimDamage);
        SubscribeLocalEvent<SeraphimBondedComponent, DamageChangedEvent>(OnPartnerDamage);

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(_ => _overdose.Clear());
    }

    #region Mend

    private void OnMendInit(Entity<SeraphimMendComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action);
        Dirty(ent);
    }

    private void OnMendShutdown(Entity<SeraphimMendComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);
    }

    private void OnMendAction(Entity<SeraphimMendComponent> ent, ref SeraphimMendActionEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;
        if (target == ent.Owner || !HasComp<LimbBodyComponent>(target) || _mobState.IsDead(target))
        {
            _popup.PopupEntity(Loc.GetString("seraphim-mend-invalid"), ent, ent);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, ent, ent.Comp.Delay, new SeraphimMendDoAfterEvent(), ent, target: target)
        {
            BreakOnMove = true,
            NeedHand = false,
            DistanceThreshold = 1.5f,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        // No cooldown yet: it only starts once the procedure is done.
        args.Handled = true;
        _audio.PlayPvs(MendSound, target);
        _popup.PopupEntity(Loc.GetString("seraphim-mend-start", ("target", target)), ent, PopupType.Medium);
    }

    private void OnMendDoAfter(Entity<SeraphimMendComponent> ent, ref SeraphimMendDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;
        MendFully(target);
        _actions.SetCooldown(ent.Comp.ActionEntity, ent.Comp.Cooldown);
        _popup.PopupEntity(Loc.GetString("seraphim-mend-done", ("target", target)), target, PopupType.Medium);
    }

    private void MendFully(EntityUid target)
    {
        for (var i = 0; i < LimbWoundRules.BodyZoneCount; i++)
        {
            _limbs.ApplyEdit(target, (LimbZone) i, new LimbEdit
            {
                SetBone = true,
                SetFlesh = true,
                SetBleed = true,
            });
        }

        for (var i = 0; i < LimbOrganRules.Count; i++)
        {
            _limbs.ApplyOrganEdit(target, (LimbOrgan) i, new LimbOrganEdit
            {
                SetDamage = true,
                SetRuptured = true,
            });
        }
    }

    #endregion

    #region Bond

    private void OnBondInit(Entity<SeraphimBondComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action);
        Dirty(ent);
    }

    private void OnBondShutdown(Entity<SeraphimBondComponent> ent, ref ComponentShutdown args)
    {
        Unbond(ent);
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);
    }

    private void OnBondAction(Entity<SeraphimBondComponent> ent, ref SeraphimBondActionEvent args)
    {
        if (args.Handled)
            return;

        // Using it again cuts the tether, whoever is clicked.
        if (ent.Comp.Partner != null)
        {
            args.Handled = true;
            Unbond(ent);
            _popup.PopupEntity(Loc.GetString("seraphim-bond-cut"), ent, ent);
            return;
        }

        var target = args.Target;
        if (target == ent.Owner ||
            !HasComp<MobStateComponent>(target) ||
            !HasComp<DamageableComponent>(target) ||
            HasComp<SeraphimBondedComponent>(target) ||
            HasComp<SeraphimBondComponent>(target) && Comp<SeraphimBondComponent>(target).Partner != null ||
            _mobState.IsDead(target))
        {
            _popup.PopupEntity(Loc.GetString("seraphim-bond-invalid"), ent, ent);
            return;
        }

        args.Handled = true;
        ent.Comp.Partner = target;
        ent.Comp.NextTick = _timing.CurTime + BondTick;
        Dirty(ent);

        EnsureComp<SeraphimBondedComponent>(target).Seraphim = ent;

        var rope = EnsureComp<JointVisualsComponent>(ent);
        rope.Sprite = ent.Comp.Rope;
        rope.Target = target;
        Dirty(ent, rope);

        _popup.PopupEntity(Loc.GetString("seraphim-bond-start", ("target", target)), ent, PopupType.Medium);
        _audio.PlayPvs(BondSound, ent);
        _audio.PlayPvs(BondSound, target);
    }

    private void Unbond(Entity<SeraphimBondComponent> ent)
    {
        if (ent.Comp.Partner is not { } partner)
            return;

        ent.Comp.Partner = null;
        if (!TerminatingOrDeleted(ent))
        {
            Dirty(ent);
            _audio.PlayPvs(BondBreakSound, ent);
        }

        if (TryComp<JointVisualsComponent>(ent, out var rope) && rope.Target == partner)
            RemComp<JointVisualsComponent>(ent);

        if (TryComp<SeraphimBondedComponent>(partner, out var bonded) && bonded.Seraphim == ent.Owner)
            RemComp<SeraphimBondedComponent>(partner);
    }

    private void OnSeraphimDamage(Entity<SeraphimBondComponent> ent, ref DamageChangedEvent args)
    {
        if (ent.Comp.Partner is { } partner)
            ShareHealing(args, partner, ent.Comp.HealShare);
    }

    private void OnPartnerDamage(Entity<SeraphimBondedComponent> ent, ref DamageChangedEvent args)
    {
        if (TryComp<SeraphimBondComponent>(ent.Comp.Seraphim, out var bond) && bond.Partner == ent.Owner)
            ShareHealing(args, ent.Comp.Seraphim, bond.HealShare);
    }

    private void ShareHealing(DamageChangedEvent args, EntityUid other, float share)
    {
        if (_mirroring || args.DamageDelta == null || share <= 0f || TerminatingOrDeleted(other))
            return;

        var healing = DamageSpecifier.GetNegative(args.DamageDelta);
        if (healing.Empty || healing.GetTotal() >= FixedPoint2.Zero)
            return;

        // The other gets as much healing, spread over whatever wounds they have, not just the same kinds.
        _mirroring = true;
        try
        {
            _damageable.HealEvenly(other, healing.GetTotal() * share);
        }
        finally
        {
            _mirroring = false;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<SeraphimBondComponent>();
        while (query.MoveNext(out var uid, out var bond))
        {
            if (bond.Partner is not { } partner || now < bond.NextTick)
                continue;

            bond.NextTick = now + BondTick;
            if (!Holds((uid, bond), partner))
            {
                Unbond((uid, bond));
                _popup.PopupEntity(Loc.GetString("seraphim-bond-snapped"), uid, uid, PopupType.MediumCaution);
                continue;
            }

            var seconds = (float) BondTick.TotalSeconds;
            MendSlowly(uid, bond, seconds);
            MendSlowly(partner, bond, seconds);
            ShareOverdose(partner, uid);
        }
    }

    private bool Holds(Entity<SeraphimBondComponent> ent, EntityUid partner)
    {
        if (TerminatingOrDeleted(partner) || _mobState.IsDead(ent) || _mobState.IsDead(partner))
            return false;

        var from = Transform(ent);
        var to = Transform(partner);
        return from.MapID == to.MapID &&
               (_transform.GetWorldPosition(from) - _transform.GetWorldPosition(to)).Length() <= ent.Comp.MaxDistance;
    }

    private void MendSlowly(EntityUid body, SeraphimBondComponent bond, float seconds)
    {
        if (!TryComp<LimbBodyComponent>(body, out var limbs))
            return;

        var count = Math.Min(limbs.Zones.Count, LimbWoundRules.BodyZoneCount);
        for (var i = 0; i < count; i++)
        {
            var zone = limbs.Zones[i];
            if (zone.Severed || zone.Bone <= 0f && zone.Bleed <= 0f)
                continue;

            _limbs.ApplyEdit(body, (LimbZone) i, new LimbEdit
            {
                Bone = -bond.BoneMendPerSecond * seconds,
                Bleed = -bond.BleedMendPerSecond * seconds,
            });
        }
    }

    /// <summary>
    /// Whatever the ally has over a reagent's overdose line is split down the middle with the seraphim,
    /// so both of them take part of the harm.
    /// </summary>
    private void ShareOverdose(EntityUid partner, EntityUid seraphim)
    {
        if (!TryComp<BloodstreamComponent>(partner, out var partnerBlood) ||
            !TryComp<BloodstreamComponent>(seraphim, out var seraphimBlood) ||
            !_solutions.ResolveSolution(partner, partnerBlood.BloodSolutionName, ref partnerBlood.BloodSolution, out var partnerSolution) ||
            !_solutions.ResolveSolution(seraphim, seraphimBlood.BloodSolutionName, ref seraphimBlood.BloodSolution, out _) ||
            partnerBlood.BloodSolution is not { } from ||
            seraphimBlood.BloodSolution is not { } to)
        {
            return;
        }

        _reagents.Clear();
        _reagents.AddRange(partnerSolution.Contents);
        foreach (var reagent in _reagents)
        {
            if (OverdoseLine(reagent.Reagent.Prototype) is not { } line || reagent.Quantity <= line)
                continue;

            var half = (reagent.Quantity - line) / 2;
            if (half <= FixedPoint2.Zero)
                continue;

            if (_solutions.TryAddReagent(to, reagent.Reagent, half, out var moved) && moved > FixedPoint2.Zero)
                _solutions.RemoveReagent(from, reagent.Reagent, moved);
        }
    }

    private FixedPoint2? OverdoseLine(string reagent)
    {
        if (_overdose.TryGetValue(reagent, out var cached))
            return cached;

        FixedPoint2? line = null;
        if (_prototypes.TryIndex<ReagentPrototype>(reagent, out var proto) && proto.Metabolisms != null)
        {
            foreach (var entry in proto.Metabolisms.Values)
            {
                foreach (var effect in entry.Effects)
                {
                    if (effect is not HealthChange health || health.Damage.GetTotal() <= FixedPoint2.Zero ||
                        effect.Conditions == null)
                    {
                        continue;
                    }

                    foreach (var condition in effect.Conditions)
                    {
                        if (condition is ReagentCondition own && own.Reagent == reagent && own.Min > FixedPoint2.Zero &&
                            (line == null || own.Min < line))
                        {
                            line = own.Min;
                        }
                    }
                }
            }
        }

        _overdose[reagent] = line;
        return line;
    }

    #endregion
}
