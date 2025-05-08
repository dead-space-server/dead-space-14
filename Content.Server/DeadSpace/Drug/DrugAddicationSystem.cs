using Content.Server.DeadSpace.Drug.Components;
using Robust.Shared.Timing;
using Content.Shared.Jittering;
using Content.Server.Speech.EntitySystems;
using Content.Shared.Stunnable;
using Content.Shared.Damage.Components;
using Content.Shared.Popups;
using Content.Server.Temperature.Systems;
using Content.Server.Temperature.Components;
using Content.Shared.Damage;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Content.Shared.Mind.Components;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Configuration;
using Content.Shared.CCVar;

namespace Content.Server.DeadSpace.Drug;

public sealed class DrugAddicationSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedJitteringSystem _sharedJittering = default!;
    [Dependency] private readonly SlurredSystem _slurred = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TemperatureSystem _temperature = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    public const float MinAddictionLevel = 1;
    public const float MinTolerance = 0.01f;
    public const float MaxAddictionLevel = 100;
    public const float MaxTolerance = 1;
    public const float MaxWithdrawalLevel = 100;
    public const float MaxAddTemperature = 10;
    public const float MaxWithdrawalRate = 5;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DrugAddicationComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<DrugAddicationComponent, ComponentShutdown>(OnComponentShut);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DrugAddicationComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (_gameTiming.CurTime > component.TimeUtilUpdate)
            {
                UpdateDrugAddication(uid, component);
                component.TimeUtilUpdate = _gameTiming.CurTime + component.UpdateDuration;
            }

            if (!component.IsTimeSendMessage && _gameTiming.CurTime > component.TimeUtilSendMessage)
                component.IsTimeSendMessage = true;

        }
    }

    private void OnComponentInit(EntityUid uid, DrugAddicationComponent component, ComponentInit args)
    {
        component.TimeUtilChangeAddiction = _gameTiming.CurTime + component.ChangeAddictionDuration;

        if (TryComp<TemperatureComponent>(uid, out var temperature))
            component.StandartTemperature = temperature.CurrentTemperature;
    }

    private void OnComponentShut(EntityUid uid, DrugAddicationComponent component, ComponentShutdown args)
    {
        RemComp<SlowedDownComponent>(uid);
    }

    public void UpdateDrugAddication(EntityUid uid, DrugAddicationComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (_mobState.IsDead(uid))
            return;

        if (component.AddictionLevel < MinAddictionLevel && component.Tolerance < MinTolerance)
            RemComp<DrugAddicationComponent>(uid);

        var time = component.UpdateDuration;
        float seconds = (float)time.TotalSeconds;

        AddTimeLastAppointment(uid, seconds, component);

        if (component.TimeLastAppointment > component.SomeThresholdTime)
        {
            // Вычисляем разницу времени с последнего приема
            var timeSinceLastAppointment = component.TimeLastAppointment - component.SomeThresholdTime;

            // Определяем прогрессию WithdrawalRate от 0 до MaxWithdrawalRate
            // Clamp для ограничения значения между 0 и 1
            float progress = Math.Clamp(timeSinceLastAppointment / component.SomeThresholdTime, 0, 1);

            // Вычисляем новый WithdrawalRate с интерполяцией
            component.WithdrawalRate = progress * MaxWithdrawalRate;

            UpdateMaxWithdrawalLevel(uid, component);
            UpdateWithdrawalLevel(uid, component);
            RunEffects(uid, component);
        }

        if (_gameTiming.CurTime > component.TimeUtilChangeAddiction)
        {
            component.AddictionLevel = Math.Max(0, component.AddictionLevel - 0.5f);
            component.Tolerance = Math.Max(0, component.Tolerance - 0.01f);
            component.TimeUtilChangeAddiction = _gameTiming.CurTime + component.ChangeAddictionDuration;
        }

    }

    public void AddAddictionLevel(EntityUid uid, float effectStrenght, DrugAddicationComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.AddictionLevel = Math.Min(MaxAddictionLevel, component.AddictionLevel + effectStrenght * (1 - component.Tolerance));

        var enableMaxAddication = _config.GetCVar(CCVars.EnableMaxAddication);
        var maxDrugStr = _config.GetCVar(CCVars.MaxDrugStr);

        if (enableMaxAddication)
            component.AddictionLevel = Math.Min(MaxAddictionLevel * component.DependencyLevel / maxDrugStr, component.AddictionLevel);
    }

    public void AddTolerance(EntityUid uid, float effectStrenght, DrugAddicationComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.Tolerance = Math.Min(MaxTolerance, component.Tolerance + effectStrenght);
    }

    public void TakeDrug(EntityUid uid, int drugStrenght, float addictionStrenght, float toleranceStrenght, DrugAddicationComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var maxDrugStr = _config.GetCVar(CCVars.MaxDrugStr);

        drugStrenght = Math.Min(maxDrugStr, drugStrenght);

        if (component.DependencyLevel <= drugStrenght)
        {
            AddAddictionLevel(uid, addictionStrenght, component);
            AddTolerance(uid, toleranceStrenght, component);
            component.DependencyLevel = drugStrenght;
            component.TimeLastAppointment = 0;
            CalculateThresholdTime(component);
        }
        else if (!component.IsTakeWeakDrug && _gameTiming.CurTime > component.DurationOfActionWeakDrug)
        {
            var strenght = drugStrenght / maxDrugStr;

            AddAddictionLevel(uid, addictionStrenght * strenght, component);
            AddTolerance(uid, toleranceStrenght * strenght, component);
            float randomFactor = Random.Shared.Next(100, 300) * strenght;
            AddTimeLastAppointment(uid, -randomFactor, component);
            component.DurationOfActionWeakDrug = _gameTiming.CurTime + TimeSpan.FromSeconds(randomFactor);
        }
    }

    public void AddTimeLastAppointment(EntityUid uid, float count, DrugAddicationComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.TimeLastAppointment += count;
        component.TimeLastAppointment = Math.Max(0, component.TimeLastAppointment);
    }

    public void UpdateWithdrawalLevel(EntityUid uid, DrugAddicationComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.WithdrawalLevel = Math.Min(MaxWithdrawalLevel, component.WithdrawalLevel + component.WithdrawalRate);
        component.WithdrawalLevel = Math.Min(component.MaxWithdrawalLvl, component.WithdrawalLevel);
    }

    public void UpdateMaxWithdrawalLevel(EntityUid uid, DrugAddicationComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        // Влияние зависимости: чем выше зависимость, тем выше MaxWithdrawalLvl
        float addictionImpact = component.AddictionLevel / MaxAddictionLevel;

        // Влияние толерантности: чем выше толерантность, тем меньше MaxWithdrawalLvl
        float toleranceImpact = MaxTolerance - (component.Tolerance / MaxTolerance);

        // Общий расчет MaxWithdrawalLvl
        component.MaxWithdrawalLvl = Math.Min(
            MaxWithdrawalLevel,
            MaxWithdrawalLevel * addictionImpact * toleranceImpact);
    }

    public void RunEffects(EntityUid uid, DrugAddicationComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.WithdrawalLevel >= 10 && component.WithdrawalLevel < 25)
        {
            if (component.IsTimeSendMessage)
            {
                _popup.PopupEntity(Loc.GetString("drug-addication-effects-low"), uid, uid);
                component.TimeUtilSendMessage = _gameTiming.CurTime + component.SendMessageDuration;
                component.IsTimeSendMessage = false;
            }

            LowEffects(uid, component);
        } else if (component.WithdrawalLevel >= 25 && component.WithdrawalLevel < 50)
        {
            if (component.IsTimeSendMessage)
            {
                _popup.PopupEntity(Loc.GetString("drug-addication-effects-medium"), uid, uid);
                component.TimeUtilSendMessage = _gameTiming.CurTime + component.SendMessageDuration;
                component.IsTimeSendMessage = false;
            }

            MediumEffects(uid, component);
        } else if (component.WithdrawalLevel >= 50 && component.WithdrawalLevel < 75)
        {
            if (component.IsTimeSendMessage)
            {
                _popup.PopupEntity(Loc.GetString("drug-addication-effects-medium-plus"), uid, uid);
                component.TimeUtilSendMessage = _gameTiming.CurTime + component.SendMessageDuration;
                component.IsTimeSendMessage = false;
            }

            MediumPlusEffects(uid, component);
        } else if (component.WithdrawalLevel >= 75)
        {
            if (component.IsTimeSendMessage)
            {
                _popup.PopupEntity(Loc.GetString("drug-addication-effects-high"), uid, uid);
                component.TimeUtilSendMessage = _gameTiming.CurTime + component.SendMessageDuration;
                component.IsTimeSendMessage = false;
            }

            HighEffects(uid, component);
        }
        else
        {
            RemComp<SlowedDownComponent>(uid);

            if (TryComp<StaminaComponent>(uid, out var stamina) && component.IsStaminaEdit)
            {
                stamina.CritThreshold /= component.StaminaMultiply;
                component.IsStaminaEdit = false;
            }
        }
    }

    private void HighEffects(EntityUid uid, DrugAddicationComponent component)
    {
        MediumPlusEffects(uid, component);
        _slurred.DoSlur(uid, TimeSpan.FromSeconds(1f) + component.UpdateDuration);
        EnsureComp<SlowedDownComponent>(uid);
        _damageable.TryChangeDamage(uid, component.Damage, true);

        if (TryComp<MindContainerComponent>(uid, out var mind)
        && TryComp<MindComponent>(mind.Mind, out var mindComp) && mindComp.Session != null)
        {
            Filter playerFilter = Filter.Empty().AddPlayer(mindComp.Session);
            _audio.PlayGlobal(component.SoundHighEffect, playerFilter, false);
        }
    }

    private void MediumPlusEffects(EntityUid uid, DrugAddicationComponent component)
    {
        _sharedJittering.DoJitter(uid, component.UpdateDuration, true, 10f * component.EffectStrengthModify);
        MediumEffects(uid, component);

        if (!TryComp<TemperatureComponent>(uid, out var temperature))
            return;

        if (temperature.CurrentTemperature > MaxAddTemperature + component.StandartTemperature)
            return;

        _temperature.ChangeHeat(uid, 4000 * component.EffectStrengthModify, true, temperature);
    }

    private void MediumEffects(EntityUid uid, DrugAddicationComponent component)
    {
        LowEffects(uid, component);

        if (!TryComp<TemperatureComponent>(uid, out var temperature))
            return;

        if (temperature.CurrentTemperature > MaxAddTemperature + component.StandartTemperature)
            return;

        _temperature.ChangeHeat(uid, 4000 * component.EffectStrengthModify, true, temperature);
    }

    private void LowEffects(EntityUid uid, DrugAddicationComponent component)
    {
        if (TryComp<StaminaComponent>(uid, out var stamina) && !component.IsStaminaEdit)
        {
            stamina.CritThreshold *= component.StaminaMultiply;
            component.IsStaminaEdit = true;
        }
    }

    public float CalculateThresholdTime(DrugAddicationComponent component)
    {
        component.SomeThresholdTime = Random.Shared.Next(300, 600);

        return component.SomeThresholdTime;
    }
}
