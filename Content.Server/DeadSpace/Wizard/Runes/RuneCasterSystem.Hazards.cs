// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Server.Temperature.Systems;
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

public sealed partial class RuneCasterSystem
{
    [Dependency] private readonly TemperatureSystem _temperature = default!;

    private const float SoakSpeed = 1.2f;
    private const float SoakKnockdown = 1.2f;
    private const float SoakInterval = 1.4f;
    private const float VacuumPopupInterval = 2f;
    private const float VampireHeal = 0.25f;
    private const string AirlossDamage = "Asphyxiation";
    private const string BruteGroup = "Brute";

    private void InitializeHazards()
    {
        SubscribeLocalEvent<RuneVacuumComponent, SpeakAttemptEvent>(OnVacuumSpeak);
    }

    private void UpdateHazards()
    {
        var now = Timing.CurTime;

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
            var choke = new DamageSpecifier();
            choke.DamageDict[AirlossDamage] = FixedPoint2.New(comp.AirlossPerSecond);
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
            if (TryComp<TemperatureComponent>(target, out var temperature))
                _temperature.ForceChangeTemperature(target, MathF.Max(0f, temperature.CurrentTemperature + effect.Delta * ctx.Power), temperature);
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
            if (!TryComp<BloodstreamComponent>(target, out var bloodstream) ||
                !_solutions.ResolveSolution(target, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var blood))
                continue;

            var normal = blood.MaxVolume.Float() / MathF.Max(1f, bloodstream.MaxVolumeModifier);
            var amount = normal * MathF.Abs(effect.Fraction) * ctx.Power;
            if (amount <= 0f)
                continue;

            if (effect.Fraction > 0f)
            {
                _bloodstream.TryModifyBloodLevel((target, bloodstream), FixedPoint2.New(amount));
                continue;
            }

            _bloodstream.TryModifyBloodLevel((target, bloodstream), FixedPoint2.New(-amount));
            if (effect.Drink <= 0f || target == ctx.Caster)
                continue;

            var drunk = amount * effect.Drink;
            _bloodstream.TryModifyBloodLevel(ctx.Caster, FixedPoint2.New(drunk));
            if (Proto.TryIndex<DamageGroupPrototype>(BruteGroup, out var brute))
                _damageable.TryChangeDamage(ctx.Caster, new DamageSpecifier(brute, FixedPoint2.New(-drunk * VampireHeal)), true, interruptsDoAfters: false);
        }
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

            var vacuum = EnsureComp<RuneVacuumComponent>(target);
            vacuum.End = end;
            vacuum.AirlossPerSecond = effect.AirlossPerSecond * ctx.Power;
            _popup.PopupEntity(Loc.GetString("rune-vacuum-start"), target, target, PopupType.MediumCaution);
        }
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
