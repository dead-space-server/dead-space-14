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
using Content.Shared.DeadSpace.CCCCVars;

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

    /// <summary>
    ///     Минимальный предел зависимости к наркотику
    /// </summary>
    private const float MinAddictionLevel = 1;

    /// <summary>
    ///     Минимальный предел толерантности к наркотику
    /// </summary>
    private const float MinTolerance = 0.01f;

    /// <summary>
    ///     Максимальный предел зависимости к наркотику
    /// </summary>
    private const float MaxAddictionLevel = 100;

    /// <summary>
    ///     Максимальный предел толерантности к наркотику
    /// </summary>
    private const float MaxTolerance = 1;

    /// <summary>
    ///     Максимальный предел тяжести эффектов
    /// </summary>
    private const float MaxWithdrawalLevel = 100;

    /// <summary>
    ///     Повышение температуры от эффекта.
    /// </summary>
    private const float MaxAddTemperature = 10;

    /// <summary>
    ///     Скорость изменение параметра тяжести эффектов. Константа, которая задает верхнюю границу скорости увеличения симптомов.
    ///     Возможно, что число не оптимально
    /// </summary>
    private const float MaxWithdrawalRate = 5;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DrugAddicationComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<DrugAddicationComponent, ComponentShutdown>(OnComponentShutdown);
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
                component.TimeUtilUpdate = _gameTiming.CurTime + TimeSpan.FromSeconds(component.UpdateDuration);
            }

            if (!component.IsTimeSendMessage && _gameTiming.CurTime > component.TimeUtilSendMessage)
                component.IsTimeSendMessage = true;

        }
    }

    private void OnComponentInit(EntityUid uid, DrugAddicationComponent component, ComponentInit args)
    {
        component.TimeUtilChangeAddiction = _gameTiming.CurTime + TimeSpan.FromSeconds(component.ChangeAddictionDuration);

        if (TryComp<TemperatureComponent>(uid, out var temperature))
            component.StandartTemperature = temperature.CurrentTemperature;
    }

    private void OnComponentShutdown(EntityUid uid, DrugAddicationComponent component, ComponentShutdown args)
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

        double seconds = TimeSpan.FromSeconds(component.UpdateDuration).TotalSeconds;

        // Нужно преобразование во float, потому-что не хочу перегружать метод
        AddTimeLastAppointment(uid, (float)seconds, component);

        if (component.TimeLastAppointment > component.SomeThresholdTime)
        {
            // Вычисляем разницу времени с последнего приема
            var timeSinceLastAppointment = component.TimeLastAppointment - component.SomeThresholdTime;

            // Определяем прогрессию WithdrawalRate от 0 до MaxWithdrawalRate
            component.WithdrawalRate = Math.Clamp(timeSinceLastAppointment / component.SomeThresholdTime, 0, MaxWithdrawalRate);

            UpdateMaxWithdrawalLevel(uid, component);
            UpdateWithdrawalLevel(uid, component);
            RunEffects(uid, component);
        }

        if (_gameTiming.CurTime > component.TimeUtilChangeAddiction)
        {
            component.AddictionLevel = Math.Max(0, component.AddictionLevel - component.AddictionLevelRegeneration);
            component.Tolerance = Math.Max(0, component.Tolerance - component.ToleranceRegeneration);
            component.TimeUtilChangeAddiction = _gameTiming.CurTime + TimeSpan.FromSeconds(component.ChangeAddictionDuration);
        }

    }

    public void AddAddictionLevel(EntityUid uid, float effectStrenght, DrugAddicationComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.AddictionLevel = Math.Min(MaxAddictionLevel, component.AddictionLevel + effectStrenght * (1 - component.Tolerance));

        var enableMaxAddication = _config.GetCVar(CCCCVars.EnableMaxAddication);
        var maxDrugStr = _config.GetCVar(CCCCVars.MaxDrugStr);

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

        var maxDrugStr = _config.GetCVar(CCCCVars.MaxDrugStr);

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

        var severity = GetSeverity(component.WithdrawalLevel);

        if (component.IsTimeSendMessage && severity != WithdrawalSeverity.None)
        {
            string msg = severity switch
            {
                WithdrawalSeverity.Low        => "drug-addication-effects-low",
                WithdrawalSeverity.Medium     => "drug-addication-effects-medium",
                WithdrawalSeverity.MediumPlus => "drug-addication-effects-medium-plus",
                WithdrawalSeverity.High       => "drug-addication-effects-high",
                _ => ""
            };

            if (!string.IsNullOrEmpty(msg))
                _popup.PopupEntity(Loc.GetString(msg), uid, uid);

            component.TimeUtilSendMessage = _gameTiming.CurTime + TimeSpan.FromSeconds(component.SendMessageDuration);
            component.IsTimeSendMessage = false;
        }

        switch (severity)
        {
            case WithdrawalSeverity.Low:
                LowEffects(uid, component);
                break;
            case WithdrawalSeverity.Medium:
                MediumEffects(uid, component);
                break;
            case WithdrawalSeverity.MediumPlus:
                MediumPlusEffects(uid, component);
                break;
            case WithdrawalSeverity.High:
                HighEffects(uid, component);
                break;
            case WithdrawalSeverity.None:
                RemComp<SlowedDownComponent>(uid);

                if (TryComp<StaminaComponent>(uid, out var stamina) && component.IsStaminaEdit)
                {
                    stamina.CritThreshold /= component.StaminaMultiply;
                    component.IsStaminaEdit = false;
                }
                break;
        }
    }

    private void HighEffects(EntityUid uid, DrugAddicationComponent component)
    {
        MediumPlusEffects(uid, component);
        _slurred.DoSlur(uid, TimeSpan.FromSeconds(1f) + TimeSpan.FromSeconds(component.UpdateDuration));
        EnsureComp<SlowedDownComponent>(uid);
        _damageable.TryChangeDamage(uid, component.Damage, true);

        if (TryComp<MindContainerComponent>(uid, out var mind)
        && TryComp<MindComponent>(mind.Mind, out var mindComp) && mindComp.Session != null)
            _audio.PlayGlobal(component.SoundHighEffect, Filter.Empty().AddPlayer(mindComp.Session), false);
    }

    private void MediumPlusEffects(EntityUid uid, DrugAddicationComponent component)
    {
        _sharedJittering.DoJitter(uid, TimeSpan.FromSeconds(component.UpdateDuration), true, 10f * component.EffectStrengthModify);
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

    private WithdrawalSeverity GetSeverity(float level)
    {
        if (level < 10)
            return WithdrawalSeverity.None;
        if (level < 25)
            return WithdrawalSeverity.Low;
        if (level < 50)
            return WithdrawalSeverity.Medium;
        if (level < 75)
            return WithdrawalSeverity.MediumPlus;

        return WithdrawalSeverity.High;
    }

    public float CalculateThresholdTime(DrugAddicationComponent component)
    {
        component.SomeThresholdTime = Random.Shared.Next(300, 600);

        return component.SomeThresholdTime;
    }
}

public enum WithdrawalSeverity
{
    None,
    Low,
    Medium,
    MediumPlus,
    High
}
