using System.Collections.Generic;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;

namespace Content.Server.DeadSpace.SaveGamemode;

/// <summary>
///     Возвращает существам то состояние, которого в файле сохранения нет: <c>MobState</c> и урон.
/// </summary>
/// <remarks>
///     <para>
///     Ни состояние, ни урон в файл не пишутся (<see cref="SavedMobStateComponent"/>), поэтому после
///     загрузки существо живое и целое, а труп при этом всё ещё гниёт — <c>RottingComponent</c>
///     маркерный и в файле остаётся. Отсюда «мёртвые существа принимают спрайт живых» и
///     «мёртвые животные возрождаются, оставаясь гнилыми».
///     </para>
///     <para>
///     Схема: перед записью карты <see cref="StampMobStates"/> навешивает метку на каждое существо,
///     после записи <see cref="ClearMobStateStamps"/> её снимает. На загрузке
///     <see cref="RestoreMobStates"/> читает метку, возвращает урон и состояние и метку удаляет.
///     Сохранения, записанные без метки, лечатся по <see cref="InferOldSaveState"/> — то есть старые
///     файлы работают без пересохранения.
///     </para>
///     <para>
///     Порядок внутри <see cref="RestoreMobStates"/> важен: сначала урон, потом состояние.
///     <c>DamageableSystem.SetDamage</c> пересчитывает <c>TotalDamage</c> и поднимает
///     <c>DamageChangedEvent</c> без дельты (ни попапов, ни крови), а <c>MobThresholdSystem.OnDamaged</c>
///     по восстановленному урону само выводит состояние — часто делать ничего не остаётся.
///     </para>
/// </remarks>
public sealed partial class SaveGamemodeMobStateSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;

    /// <summary>
    ///     Навешивает метку состояния на каждое существо перед сериализацией карты.
    /// </summary>
    /// <returns>Сколько существ получили метку.</returns>
    /// <remarks>
    ///     Проходит по всему серверу, а не по одной карте: сетки, пришедшие в файле отдельным списком
    ///     (например, пристыкованный шаттл), живут на своих MapId, и существовать «вне карты» — нормально.
    ///     Существо без <c>MobStateComponent</c> метки не получает: его состояние в файле есть.
    ///     Вызывается только на время записи, см. <see cref="ClearMobStateStamps"/>.
    /// </remarks>
    public int StampMobStates()
    {
        var stamped = 0;

        // Список, а не обход запроса: EnsureComp меняет состав сущностей прямо во время итерации.
        var mobs = new List<Entity<MobStateComponent>>();
        var query = AllEntityQuery<MobStateComponent>();
        while (query.MoveNext(out var uid, out var mobState))
            mobs.Add((uid, mobState));

        foreach (var (uid, mobState) in mobs)
        {
            if (TerminatingOrDeleted(uid))
                continue;

            var saved = EnsureComp<SavedMobStateComponent>(uid);
            saved.State = mobState.CurrentState;
            saved.Damage.Clear();

            if (!TryComp<DamageableComponent>(uid, out var damageable))
                continue;

            foreach (var (type, value) in damageable.Damage.DamageDict)
            {
                // Ноль — не состояние, а шум в файле: поле урона пишется как словарь, нули в нём
                // только раздувают сохранение.
                if (value != FixedPoint2.Zero)
                    saved.Damage[type] = value;
            }

            stamped++;
        }

        return stamped;
    }

    /// <summary>
    ///     Снимает метки состояния: в живом мире их быть не должно.
    /// </summary>
    /// <returns>Сколько меток снято.</returns>
    public int ClearMobStateStamps()
    {
        var stamps = new List<Entity<SavedMobStateComponent>>();
        var query = AllEntityQuery<SavedMobStateComponent>();
        while (query.MoveNext(out var uid, out var comp))
            stamps.Add((uid, comp));

        foreach (var (uid, comp) in stamps)
            RemCompDeferred(uid, comp);

        return stamps.Count;
    }

    /// <summary>
    ///     Возвращает существам состояние и урон, которых нет в файле сохранения.
    /// </summary>
    /// <returns>Сколько существ вернули состояние и сколько — урон.</returns>
    /// <remarks>
    ///     Существо берём всё то же «по всему серверу»: часть сеток станция забирает к себе позже,
    ///     а загрузка чужих файлов (следующая карта раунда) чинится этим же проходом.
    ///     Для существа без метки состояние выводится из того, что в файле всё-таки осталось,
    ///     см. <see cref="InferOldSaveState"/>.
    ///     Смена состояния усыпляет HTN (<c>NPCSystem.OnMobStateChange</c>), поэтому проход обязан
    ///     идти ДО <c>WakeLoadedNpcs</c> — иначе разбуженный труп снова оживает для поведения.
    /// </remarks>
    public (int Restored, int Damaged) RestoreMobStates()
    {
        var restored = 0;
        var damaged = 0;

        var mobs = new List<Entity<MobStateComponent>>();
        var query = AllEntityQuery<MobStateComponent>();
        while (query.MoveNext(out var uid, out var mobState))
            mobs.Add((uid, mobState));

        foreach (var (uid, mobState) in mobs)
        {
            if (TerminatingOrDeleted(uid))
                continue;

            MobState state;
            Dictionary<string, FixedPoint2>? damage = null;

            if (TryComp(uid, out SavedMobStateComponent? stamp))
            {
                state = stamp.State;
                if (stamp.Damage.Count > 0)
                    damage = stamp.Damage;

                RemCompDeferred(uid, stamp);
            }
            else
            {
                state = InferOldSaveState(uid);
            }

            if (state == MobState.Invalid)
                continue;

            // Урон — первым: по нему пороги сами выводят состояние (см. remarks класса).
            if (damage != null && TryComp<DamageableComponent>(uid, out var damageable))
            {
                _damageable.SetDamage((uid, damageable), new DamageSpecifier
                {
                    DamageDict = new Dictionary<string, FixedPoint2>(damage),
                });
                damaged++;
            }

            // Состояние могло вернуться вместе с уроном; сверяем с тем, что было на момент сохранения.
            // HasState — не перестраховка: у части существ (синты, некоторые существа) набора состояний
            // не хватает, а ChangeState на неразрешённое состояние просто молча ничего не делает.
            if (state != mobState.CurrentState && _mobState.HasState(uid, state, mobState))
            {
                _mobState.ChangeMobState(uid, state, mobState);
                restored++;
            }

            // Порог закрепляем сохранённым состоянием. OnUpdateMobState смотрит на CurrentThresholdState,
            // и без закрепления существо вернул бы состояние первый же удар: у трупа в файле порог Dead
            // остался, а состояние — Alive, значит TriggerThreshold(Alive) оживил бы его при уроне 0.
            if (TryComp<MobThresholdsComponent>(uid, out var thresholds))
                _mobThreshold.SetCurrentThresholdState(uid, state, thresholds);
        }

        return (restored, damaged);
    }

    /// <summary>
    ///     Состояние существа для сохранения, в котором метки нет — файл записан до появления метки.
    /// </summary>
    /// <remarks>
    ///     Опирается только на то, что в файле осталось:
    ///     <list type="bullet">
    ///     <item>
    ///     <c>MobThresholdsComponent.CurrentThresholdState</c> — единственное состояние, которое пишется
    ///     (<c>[DataField("currentThresholdState")]</c>, не readOnly). В живой игре оно идёт рядом с
    ///     <c>CurrentState</c>, а <c>TriggerThreshold</c> не даёт ему отойти от Dead, пока существо мертво
    ///     и не разрешены воскрешения — то есть для «был мёртв» оно точное.
    ///     </item>
    ///     <item>
    ///     <c>RottingComponent</c> — гниение начинается только у мёртвых, а при выходе из Dead
    ///     <c>SharedRottingSystem</c> его снимает. Значит существо было мертво.
    ///     По <c>PerishableComponent.RotAccumulator</c> судить нельзя: при снятии гниения (реанимация,
    ///     <c>ReduceAccumulator</c> на операции) накопленное время остаётся.
    ///     </item>
    ///     </list>
    ///     Ни порогов, ни гниения нет — считаем живым: угадывать нечем, а оживление — то, что происходило
    ///     и раньше.
    /// </remarks>
    private MobState InferOldSaveState(EntityUid uid)
    {
        if (TryComp<MobThresholdsComponent>(uid, out var thresholds))
        {
            var threshold = thresholds.CurrentThresholdState;
            if (threshold != MobState.Invalid)
                return threshold;
        }

        if (_mobState.HasState(uid, MobState.Dead) && HasComp<RottingComponent>(uid))
            return MobState.Dead;

        return MobState.Alive;
    }
}
