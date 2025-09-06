using Content.Server.DeadSpace.MentalIllness.Components;
using Content.Shared.DeadSpace.MentalIllness;
using Robust.Shared.Timing;
using Robust.Shared.Audio.Systems;
using Content.Server.Chat.Systems;
using Robust.Shared.Random;
using Content.Shared.Popups;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.DeadSpace.Necromorphs.Sanity;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Jittering;
using Content.Shared.Zombies;
using Content.Shared.DeadSpace.Necromorphs.InfectionDead.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.MentalIllness;

public sealed partial class MentalIllnessSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedSanitySystem _sanity = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedJitteringSystem _sharedJittering = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MentalIllnessComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<MentalIllnessComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<MentalIllnessComponent, RefreshMovementSpeedModifiersEvent>(OnRefresh);
        SubscribeLocalEvent<MentalIllnessComponent, MeleeHitEvent>(OnMeleeHit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MentalIllnessComponent>();
        while (query.MoveNext(out var ent, out var component))
        {
            if (_gameTiming.CurTime > component.TimeUntilUpdate)
            {
                UpdateMental(ent, component);
                component.TimeUntilUpdate = _gameTiming.CurTime + TimeSpan.FromSeconds(1);
            }

        }
    }

    /// <summary>
    ///     Инициализация компонента психического заболевания при его добавлении на сущность.
    ///     Устанавливает начальные интервалы и случайные болезни, если это требуется.
    /// </summary>
    private void OnComponentInit(EntityUid uid, MentalIllnessComponent component, ComponentInit args)
    {
        component.TimeUntilUpdate = _gameTiming.CurTime + TimeSpan.FromSeconds(1);

        if (component.RandomOnInitialization)
        {
            // Получаем случайный тип болезни
            var randomIllness = GetRandomIllnessType();

            // Добавляем случайное заболевание с указанной тяжестью
            TryAddIllness(uid, randomIllness, component.SeverityOnInitialization, component);
        }

        // Перебираем все активные заболевания и создаем для них случайный интервал времени
        foreach (var illness in component.ActiveIllnesses)
        {
            // Рассчитываем случайный интервал для заболевания
            var illnessInterval = GetRandomIllnessInterval(uid, illness, component.MinTickIntervals, component.MaxTickIntervals);

            // Сохраняем рассчитанный интервал в словарь
            component.IllnessTickIntervals[illness] = illnessInterval;
        }
    }

    private void OnShutdown(EntityUid uid, MentalIllnessComponent component, ComponentShutdown args)
    {
        component.MovementSpeedModifier = 1f;
        _movement.RefreshMovementSpeedModifiers(uid);
    }

    private void OnRefresh(EntityUid uid, MentalIllnessComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(component.MovementSpeedModifier, component.MovementSpeedModifier);
    }

    /// <summary>
    ///     Обрабатывает урон от ближней атаки с учётом психического состояния.
    /// </summary>
    private void OnMeleeHit(EntityUid uid, MentalIllnessComponent component, MeleeHitEvent args)
    {
        component.DamageMultiply = 1f * component.DepressionDamageMultiply * component.ManiaDamageMultiply;

        var message = Loc.GetString("depression-melee-hit");

        if (component.DamageMultiply < 1)
            _popup.PopupEntity(message, uid, uid);

        args.BonusDamage = args.BaseDamage * component.DamageMultiply;
    }

    /// <summary>
    ///     Обновляет состояние психического здоровья сущности и активирует эффекты болезней.
    /// </summary>
    public void UpdateMental(EntityUid uid, MentalIllnessComponent? component = null)
    {
        if (!Resolve(uid, ref component, true))
            return;

        if (!_mobState.IsAlive(uid))
            return;

        if (HasComp<ZombieComponent>(uid) || HasComp<NecromorfComponent>(uid))
            RemComp<MentalIllnessComponent>(uid);

        if (component.MedicinesEffect > 0)
            component.MedicinesEffect -= component.MedicinesEffectRegen;

        foreach (var illness in component.ActiveIllnesses)
        {

            // Проверяем, если время для обновления заболевания пришло
            if (_gameTiming.CurTime > component.IllnessTickIntervals[illness])
            {
                // Обновляем состояние заболевания
                switch (illness)
                {
                    case MentalIllnessType.Paranoia:
                        TriggerParanoiaEffects(uid, component);
                        break;
                    case MentalIllnessType.Depression:
                        TriggerDepressionEffects(uid, component);
                        break;
                    case MentalIllnessType.Mania:
                        TriggerManiaEffects(uid, component);
                        break;
                    case MentalIllnessType.Anxiety:
                        TriggerAnxietyEffects(uid, component);
                        break;
                }

                AddIllnessSeverity(uid, illness, component.SeverityRegen);
                // Устанавливаем новое время для заболевания с новым случайным интервалом
                var minInterval = component.MinTickIntervals;
                var maxInterval = component.MaxTickIntervals;
                component.IllnessTickIntervals[illness] = _gameTiming.CurTime + GetRandomIllnessInterval(uid, illness, minInterval, maxInterval);
            }
        }
    }

    /// <summary>
    ///     Увеличивает время тика для указанного заболевания.
    /// </summary>
    public void AddIllnessTickIntervals(EntityUid uid, MentalIllnessType illness, float seconds, MentalIllnessComponent? component = null)
    {
        if (!Resolve(uid, ref component, true))
            return;

        if (!component.IllnessTickIntervals.TryGetValue(illness, out var illnessIntervals))
            return;

        illnessIntervals = illnessIntervals + TimeSpan.FromSeconds(seconds);
        component.IllnessTickIntervals[illness] = illnessIntervals;
    }

    /// <summary>
    ///     Увеличивает или уменьшает тяжесть заболевания. Может учитывать эффект лекарства.
    /// </summary>
    public void AddIllnessSeverity(EntityUid uid, MentalIllnessType illness, float severity, bool isMedecin = false, MentalIllnessComponent? component = null)
    {
        if (!Resolve(uid, ref component, true))
            return;

        if (!component.Severity.TryGetValue(illness, out var illnessSeverity))
            return;

        component.Severity[illness] = illnessSeverity + severity - component.MedicinesEffect * severity;

        if (isMedecin)
        {
            component.MedicinesEffect += severity;
            component.MedicinesEffect = Math.Min(1f, component.MedicinesEffect);
        }
    }

    /// <summary>
    ///     Добавляет новое психическое заболевание на сущность.
    /// </summary>
    public bool TryAddIllness(EntityUid uid, MentalIllnessType illness, float severity, MentalIllnessComponent? component = null)
    {
        if (!Resolve(uid, ref component, true))
            return false;

        // Если заболевание еще не активировано, добавляем его в список активных заболеваний
        if (component.ActiveIllnesses.Contains(illness))
            return false;

        component.ActiveIllnesses.Add(illness);
        // Устанавливаем степень заболевания
        component.Severity[illness] = severity;

        var illnessInterval = GetRandomIllnessInterval(uid, illness, component.MinTickIntervals, component.MaxTickIntervals);

        // Добавляем рассчитанный интервал в IllnessTickIntervals
        component.IllnessTickIntervals[illness] = illnessInterval;

        return true;
    }

    /// <summary>
    ///     Возвращает случайный интервал между эффектами заболевания.
    /// </summary>
    public TimeSpan GetRandomIllnessInterval(EntityUid uid, MentalIllnessType illness, float minInterval, float maxInterval, MentalIllnessComponent? component = null)
    {
        if (!Resolve(uid, ref component, true))
            return TimeSpan.Zero;

        var randomValue = _random.NextFloat(minInterval, maxInterval) / component.Severity[illness];
        return TimeSpan.FromSeconds(randomValue);
    }

    /// <summary>
    ///     Возвращает случайный тип психического заболевания.
    /// </summary>
    public MentalIllnessType GetRandomIllnessType()
    {
        // Получаем все доступные типы заболеваний
        var allIllnesses = Enum.GetValues<MentalIllnessType>();

        // Возвращаем случайное заболевание
        return _random.Pick(allIllnesses);
    }

    /// <summary>
    ///     Пересчитывает итоговый множитель скорости движения на основе заболеваний.
    /// </summary>
    public void RefreshTotalSpeed(EntityUid uid, MentalIllnessComponent? component = null)
    {
        if (!Resolve(uid, ref component, true))
            return;

        component.MovementSpeedModifier = 1f * component.ManiaMovementSpeedModifier * component.DepressionMovementSpeedModifier;
        _movement.RefreshMovementSpeedModifiers(uid);
    }
}
