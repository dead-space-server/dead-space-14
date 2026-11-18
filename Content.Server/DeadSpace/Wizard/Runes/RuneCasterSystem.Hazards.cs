// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.Temperature.Systems;
using Content.Shared.Atmos;
using Content.Shared.Body.Components;
using Content.Shared.Chat;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Wizard.Runes;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Speech;
using Content.Shared.Temperature.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Wizard.Runes;

[RegisterComponent]
public sealed partial class RuneSoakedComponent : Component
{
    public TimeSpan End;

    public TimeSpan NextSlip;
}

[RegisterComponent]
public sealed partial class RuneVacuumComponent : Component
{
    public TimeSpan End;

    public TimeSpan NextTick;

    public TimeSpan NextPopup;

    public float AirlossPerSecond = 1f;

    public float Cap;

    public FixedPoint2 Start;
}

[RegisterComponent]
public sealed partial class RuneReagentContactComponent : Component
{
    [DataField]
    public ProtoId<ReagentPrototype>? Reagent;

    [DataField]
    public float Amount = 0.5f;

    [DataField]
    public bool Extinguish;

    [DataField]
    public float Interval = 1f;

    [DataField]
    public float Radius = 0.5f;

    public TimeSpan Next;
}

[RegisterComponent]
public sealed partial class RuneBlazeComponent : Component
{
    [DataField]
    public float Lifetime = 20f;

    public TimeSpan End;
}

public sealed partial class RuneCasterSystem
{
    [Dependency] private readonly TemperatureSystem _temperature = default!;

    private const float SoakSpeed = 1.2f;
    private const float SoakKnockdown = 1.2f;
    private const float SoakInterval = 1.4f;
    private const float VacuumPopupInterval = 2f;
    private const float VampireHeal = 0.35f;
    private const float VampireHealCap = 35f;
    private const string AirlossDamage = "Asphyxiation";
    private const string BruteGroup = "Brute";

    private void InitializeHazards()
    {
        SubscribeLocalEvent<RuneVacuumComponent, SpeakAttemptEvent>(OnVacuumSpeak);
        SubscribeLocalEvent<RuneVacuumComponent, InhaleLocationEvent>(OnVacuumInhale, after: new[] { typeof(InternalsSystem) });
        SubscribeLocalEvent<RuneBlazeComponent, MapInitEvent>(OnBlazeInit);
    }

    private void OnVacuumInhale(Entity<RuneVacuumComponent> ent, ref InhaleLocationEvent args)
    {
        args.Gas = new GasMixture(args.Respirator.BreathVolume);
    }

    private void OnBlazeInit(Entity<RuneBlazeComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.End = Timing.CurTime + TimeSpan.FromSeconds(ent.Comp.Lifetime);
        foreach (var other in _lookup.GetEntitiesInRange<RuneBlazeComponent>(_transform.GetMapCoordinates(ent), 1.5f))
        {
            if (other.Owner != ent.Owner && other.Comp.End > TimeSpan.Zero && other.Comp.End < ent.Comp.End)
                ent.Comp.End = other.Comp.End;
        }
    }

    private void UpdateHazards()
    {
        var now = Timing.CurTime;

        var blazes = EntityQueryEnumerator<RuneBlazeComponent>();
        while (blazes.MoveNext(out var uid, out var blaze))
        {
            if (now >= blaze.End)
                QueueDel(uid);
        }

        var soaked = EntityQueryEnumerator<RuneSoakedComponent, PhysicsComponent>();
        while (soaked.MoveNext(out var uid, out var soak, out var physics))
        {
            if (now >= soak.End)
            {
                RemCompDeferred<RuneSoakedComponent>(uid);
                continue;
            }

            if (now < soak.NextSlip || physics.LinearVelocity.Length() < SoakSpeed || _standing.IsDown(uid))
                continue;

            soak.NextSlip = now + TimeSpan.FromSeconds(SoakInterval);
            _stun.TryKnockdown(uid, TimeSpan.FromSeconds(SoakKnockdown));
            _popup.PopupEntity(Loc.GetString("rune-soak-slip"), uid, uid, PopupType.SmallCaution);
        }

        var vacuum = EntityQueryEnumerator<RuneVacuumComponent>();
        while (vacuum.MoveNext(out var uid, out var comp))
        {
            if (now >= comp.End || _mobState.IsDead(uid))
            {
                RemCompDeferred<RuneVacuumComponent>(uid);
                continue;
            }

            if (now < comp.NextTick)
                continue;

            comp.NextTick = now + TimeSpan.FromSeconds(1);
            var dealt = (Asphyxiation(uid) - comp.Start).Float();
            if (dealt >= comp.Cap)
            {
                RemCompDeferred<RuneVacuumComponent>(uid);
                continue;
            }

            if (HasComp<RespiratorComponent>(uid))
                continue;

            var choke = new DamageSpecifier();
            choke.DamageDict[AirlossDamage] = FixedPoint2.New(MathF.Min(comp.AirlossPerSecond, comp.Cap - dealt));
            _damageable.TryChangeDamage(uid, choke, true, interruptsDoAfters: false);
        }

        var contacts = EntityQueryEnumerator<RuneReagentContactComponent, TransformComponent>();
        while (contacts.MoveNext(out _, out var contact, out var xform))
        {
            if (now < contact.Next)
                continue;

            contact.Next = now + TimeSpan.FromSeconds(contact.Interval);
            foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(_transform.GetMapCoordinates(xform), contact.Radius))
            {
                if (contact.Reagent is { } reagent)
                    Inject(mob, reagent, contact.Amount);
                if (contact.Extinguish)
                    _flammable.Extinguish(mob);
            }
        }
    }

    private void OnVacuumSpeak(Entity<RuneVacuumComponent> ent, ref SpeakAttemptEvent args)
    {
        args.Cancel();
        if (Timing.CurTime < ent.Comp.NextPopup)
            return;

        ent.Comp.NextPopup = Timing.CurTime + TimeSpan.FromSeconds(VacuumPopupInterval);
        _popup.PopupEntity(Loc.GetString("rune-vacuum-mute"), ent, ent, PopupType.SmallCaution);
    }

    private void ApplyTemperature(RuneCastContext ctx, RuneTemperatureEffect effect)
    {
        foreach (var target in Targets(ctx, effect))
        {
            if (effect.Set is { } set)
            {
                if (TryComp<TemperatureComponent>(target, out var temperature))
                    _temperature.ForceChangeTemperature(target, MathF.Max(0f, set), temperature);
                continue;
            }

            ShiftTemperature(target, effect.Delta * ctx.Power);
        }
    }

    private void ApplyInject(RuneCastContext ctx, RuneInjectEffect effect)
    {
        foreach (var target in Targets(ctx, effect))
            Inject(target, effect.Reagent, effect.Amount * ctx.Power);
    }

    private void Inject(EntityUid target, ProtoId<ReagentPrototype> reagent, float amount)
    {
        if (amount <= 0f || !TryComp<BloodstreamComponent>(target, out var bloodstream))
            return;

        _bloodstream.TryAddToBloodstream((target, bloodstream), new Solution(reagent, FixedPoint2.New(amount)));
    }

    private void ApplyBlood(RuneCastContext ctx, RuneBloodEffect effect)
    {
        foreach (var target in Targets(ctx, effect))
        {
            var fraction = MathF.Abs(effect.Fraction) * ctx.Power;
            if (effect.Fraction < 0f)
            {
                DrainBlood(ctx.Caster, target, fraction, effect.Drink);
                continue;
            }

            if (TryNormalBlood(target, out var bloodstream, out var normal))
                _bloodstream.TryModifyBloodLevel((target, bloodstream), FixedPoint2.New(normal * fraction));
        }
    }

    private bool TryNormalBlood(EntityUid target, [NotNullWhen(true)] out BloodstreamComponent? bloodstream, out float normal)
    {
        normal = 0f;
        if (!TryComp(target, out bloodstream) ||
            !_solutions.ResolveSolution(target, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var blood))
            return false;

        normal = blood.MaxVolume.Float() / MathF.Max(1f, bloodstream.MaxVolumeModifier);
        return normal > 0f;
    }

    private void DrainBlood(EntityUid caster, EntityUid target, float fraction, float drink)
    {
        if (fraction <= 0f || !TryNormalBlood(target, out var bloodstream, out var normal))
            return;

        var amount = normal * fraction;
        _bloodstream.TryModifyBloodLevel((target, bloodstream), FixedPoint2.New(-amount));
        if (drink <= 0f || target == caster)
            return;

        var drunk = amount * drink;
        _bloodstream.TryModifyBloodLevel(caster, FixedPoint2.New(drunk));
        var heal = MathF.Min(drunk * VampireHeal, VampireHealCap);
        if (Proto.TryIndex<DamageGroupPrototype>(BruteGroup, out var brute) && Proto.TryIndex(BurnGroup, out var burn))
        {
            var mend = new DamageSpecifier(brute, FixedPoint2.New(-heal)) + new DamageSpecifier(burn, FixedPoint2.New(-heal * 0.5f));
            _damageable.TryChangeDamage(caster, mend, true, interruptsDoAfters: false);
        }

        _popup.PopupEntity(Loc.GetString("rune-blood-drink"), caster, caster, PopupType.Small);
    }

    private void ApplyDisarm(RuneCastContext ctx, RuneDisarmEffect effect)
    {
        foreach (var target in Targets(ctx, effect))
        {
            if (target == ctx.Caster)
                continue;

            foreach (var held in _hands.EnumerateHeld(target).ToList())
                _hands.TryDrop(target, held, checkActionBlocker: false);
        }
    }

    private void ApplyEmote(RuneCastContext ctx, RuneEmoteEffect effect)
    {
        var text = Loc.GetString(effect.Emote);
        foreach (var target in Targets(ctx, effect))
        {
            if (target != ctx.Caster)
                _chat.TrySendInGameICMessage(target, text, InGameICChatType.Emote, false, ignoreActionBlocker: true);
        }
    }

    private void ApplyLightningStrike(RuneCastContext ctx, RuneLightningStrikeEffect effect)
    {
        _lightningOwner = ctx.Caster;
        try
        {
            foreach (var target in Targets(ctx, effect))
            {
                if (target != ctx.Caster)
                    _lightning.ShootLightning(ctx.Caster, target, effect.Prototype);
            }
        }
        finally
        {
            _lightningOwner = null;
        }
    }

    private void ApplySoak(RuneCastContext ctx, RuneSoakEffect effect)
    {
        var end = Timing.CurTime + TimeSpan.FromSeconds(effect.Time * ctx.DurationScale);
        foreach (var target in Targets(ctx, effect))
        {
            if (target == ctx.Caster)
                continue;

            var soak = EnsureComp<RuneSoakedComponent>(target);
            if (soak.End < end)
                soak.End = end;
        }
    }

    private void ApplyVacuum(RuneCastContext ctx, RuneVacuumEffect effect)
    {
        var end = Timing.CurTime + TimeSpan.FromSeconds(effect.Time * ctx.DurationScale);
        foreach (var target in Targets(ctx, effect))
        {
            if (target == ctx.Caster)
                continue;

            var fresh = !HasComp<RuneVacuumComponent>(target);
            var vacuum = EnsureComp<RuneVacuumComponent>(target);
            vacuum.End = end;
            vacuum.AirlossPerSecond = effect.AirlossPerSecond * ctx.Power;
            vacuum.Cap = effect.Time * effect.AirlossPerSecond * ctx.Power;
            if (fresh)
                vacuum.Start = Asphyxiation(target);
            _popup.PopupEntity(Loc.GetString("rune-vacuum-start"), target, target, PopupType.MediumCaution);
        }
    }

    private FixedPoint2 Asphyxiation(EntityUid uid)
    {
        return TryComp<DamageableComponent>(uid, out var damageable) && damageable.Damage.DamageDict.TryGetValue(AirlossDamage, out var value)
            ? value
            : FixedPoint2.Zero;
    }

    private float StaminaAmount(RuneCastContext ctx, EntityUid target, RuneStaminaEffect effect)
    {
        var amount = effect.Amount * ctx.Power;
        if (effect.ResistAs is not { } type || !Proto.TryIndex(type, out var damageType))
            return amount * Touch(ctx, target);

        var damage = new DamageSpecifier(damageType, FixedPoint2.New(amount));
        if (TryComp<DamageableComponent>(target, out var damageable) &&
            damageable.DamageModifierSetId is { } set &&
            Proto.TryIndex(set, out var modifier))
            damage = DamageSpecifier.ApplyModifierSet(damage, modifier);

        var ev = new DamageModifyEvent(damage, ctx.Caster);
        RaiseLocalEvent(target, ev);
        return MathF.Max(0f, ev.Damage.GetTotal().Float());
    }
}
