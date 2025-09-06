using Robust.Shared.Timing;
using Content.Server.DeadSpace.Asthma.Components;
using Robust.Shared.Random;
using Content.Shared.Mobs.Systems;
using Content.Server.Chat.Systems;
using Content.Shared.Damage;
using Content.Shared.Speech.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Stunnable;
using Content.Shared.StatusEffect;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server.DeadSpace.Asthma;

public sealed class AsthmaSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffect = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AsthmaComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<AsthmaComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<AsthmaComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<AsthmaComponent, DamageChangedEvent>(OnDamageChange);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<AsthmaComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (_timing.CurTime > component.TimeToStartEffect.Remaining)
            {
                if (_timing.CurTime > component.TimeToStartAsthmaAttack.Remaining)
                {
                    TriggerAsthmaEffect(uid, component);
                }
            }
        }
    }

    private void OnMeleeHit(EntityUid uid, AsthmaComponent component, MeleeHitEvent args)
    {
        ReduceAsthmaEffectTimer(uid, component.AsthmaAttackTimeReductionOnMelee, component);
    }

    private void OnDamageChange(EntityUid uid, AsthmaComponent component, DamageChangedEvent args)
    {
        if (args.DamageDelta != null && args.DamageDelta.GetTotal().Float() > 5f)
            ReduceAsthmaEffectTimer(uid, component.AsthmaAttackTimeReductionOnDamageChanged, component);

    }

    private void OnComponentInit(EntityUid uid, AsthmaComponent component, ComponentInit args)
    {
        UpdateTimeWindow(ref component.TimeToStartEffect);
    }

    private void OnComponentShutdown(EntityUid uid, AsthmaComponent component, ComponentShutdown args)
    {
        component.MovementSpeedMultiplier = 1f;
        _movement.RefreshMovementSpeedModifiers(uid);
    }
    private void OnRefresh(EntityUid uid, AsthmaComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(component.MovementSpeedMultiplier, component.MovementSpeedMultiplier);
    }

    private void TriggerAsthmaEffect(EntityUid uid, AsthmaComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (_mobState.IsDead(uid))
            return;

        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return;

        _damageable.TryChangeDamage(uid, component.AdditionalDamage, true, false, damageable);

        component.IsAsthmaAttack = true;

        if (TryComp<VocalComponent>(uid, out var vocal))
        {
            var message = Loc.GetString("emote-asthma-gasp", ("random", _random.Next(1, 6)));
            _chat.TryPlayEmoteSound(uid, vocal.EmoteSounds, "Gasp");
            _chat.TrySendInGameICMessage(uid, message, InGameICChatType.Emote, ChatTransmitRange.Normal);
        }

        component.MovementSpeedMultiplier = component.SpeedDebuff;
        _movement.RefreshMovementSpeedModifiers(uid);

        if (_random.NextDouble() <= component.StunChance)
            _statusEffect.TryAddStatusEffect<StunnedComponent>(uid, "Stun", TimeSpan.FromSeconds(component.TimeToStartAsthmaAttack.MinSeconds), true);

        UpdateTimeWindow(ref component.TimeToStartAsthmaAttack);
    }

    private void UpdateTimeWindow(ref TimedWindow timeWindow)
    {
        timeWindow.Remaining = _timing.CurTime + GetRandomDuration(timeWindow);
    }

    private TimeSpan GetRandomDuration(TimedWindow window)
    {
        return TimeSpan.FromSeconds(_random.NextFloat(window.MinSeconds, window.MaxSeconds));
    }

    private void ReduceAsthmaEffectTimer(EntityUid uid, TimedWindow timeWindow, AsthmaComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.TimeToStartEffect.Remaining -= GetRandomDuration(timeWindow);
    }
    public void ResetAsthma(EntityUid uid, AsthmaComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        UpdateTimeWindow(ref component.TimeToStartEffect);

        component.MovementSpeedMultiplier = 1f;
        _movement.RefreshMovementSpeedModifiers(uid);
        component.IsAsthmaAttack = false;
    }
}
