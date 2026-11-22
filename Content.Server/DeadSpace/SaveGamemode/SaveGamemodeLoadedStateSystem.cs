using System;
using System.Collections.Generic;
using Content.Shared.Materials;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.GameObjects;

namespace Content.Server.DeadSpace.SaveGamemode;

/// <summary>
///     Снимает с загруженной карты состояние переходящих процессов: и то, что в файле лежать не должно,
///     и то, чего в файле нет, но навёл бы MapInit.
/// </summary>
/// <remarks>
///     <para>
///     Один проход закрывает три разных бага — у них общий корень: карта поднимается без MapInit
///     (<c>NormalizeMapInitFlags</c>), а внешний вид и прогресс переходящих процессов в файл не пишутся.
///     </para>
///     <list type="number">
///     <item><description>
///     <b>Маркер сериализовался «голым» узлом.</b> <c>LatheProducingComponent</c> полей-<c>DataField</c>
///     не имеет, но тип компонента в файл попадает: <c>EntitySerializer</c> пишет узел и без полей.
///     На загрузке его <c>ComponentStartup</c> снова включает «рабочий» режим машины
///     (<c>LatheProducingSystem</c>), а прогресс (<c>StartTime</c>, <c>ProductionLength</c>) не сериализован
///     и <c>CurrentRecipe</c> после загрузки нулевой → <c>LatheSystem.Update</c> пропускает станок,
///     до <c>FinishProducing</c> дело не доходит и маркер не снимается никогда.
///     </description></item>
///     <item><description>
///     <b>Ключа внешнего вида нет, а слой в прототипе видим.</b> Внешний вид в файл не пишется вообще
///     (<c>AppearanceComponent.AppearanceDataInit</c> — <c>readOnly</c>), а клиентский визуализатор при
///     отсутствии ключа оставляет слой таким, как он написан в прототипе
///     (<c>Content.Client/Materials/MaterialStorageSystem.cs:27</c>). Слой «inserting» у автолатов и
///     протолатов был написан без <c>visible: false</c> и проигрывался вечно: скрыть его мог только
///     <c>SharedMaterialStorageSystem.OnMapInit</c>, а MapInit не приходил. Отсюда «в них постоянно что-то
///     засовывают». YAML с <c>visible: false</c> — основное лекарство (так же, как у flatpacker и
///     аномального генератора), этот проход — страховка на случай прототипа, где дефолт снова забыт.
///     </description></item>
///     <item><description>
///     <b>Наоборот, протухшее поле приехало из файла.</b> <c>PowerStateComponent.IsWorking</c> — обычный
///     <c>[DataField]</c>, то есть работает в файле. Машина, работавшая в момент сохранения, и после
///     загрузки тянет рабочую нагрузку вместо холостой; маркера переходящего процесса у неё может уже и не быть.
///     </description></item>
///     </list>
///     <para>
///     Новый случай этого класса — одна строка в одном из списков ниже, а не новая система.
///     Классы «в файле нет состояния существа» и «в файле нет runtime-словаря» лечатся не здесь:
///     см. <see cref="SaveGamemodeMobStateSystem"/> и починки в <see cref="SaveGamemodeRuleSystem"/>.
///     </para>
/// </remarks>
public sealed partial class SaveGamemodeLoadedStateSystem : EntitySystem
{
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPowerStateSystem _powerState = default!;

    /// <summary>
    ///     Маркерные компоненты переходящих процессов: на загрузке снимаются со всех сущностей.
    ///     Побочный эффект отменяет их же <c>ComponentShutdown</c>, поэтому руками ничего не гасим.
    /// </summary>
    private static readonly string[] TransientComponents =
    [
        // «Станок печатает». Прогресс не сериализуется, поэтому печатать он будет вечно.
        // Снятие вызывает TrySetWorkingState(false) — рабочий режим и нагрузка гасятся сами.
        // Очередь заданий (Lathe.Queue) не трогаем: это обычное DataField, и на первом тике питания
        // LatheSystem.OnPowerChanged сам продолжит печать (Powered — [ViewVariables], в файле его нет).
        "LatheProducing",
        // «В станок вставляют материал». EndTime — абсолютное время ТОГО сервера, и по [AutoPausedField]
        // он либо уехал в бесконечность, либо истёк; в обоих случаях доводка процесса не дойдёт.
        "InsertingMaterialStorage",
    ];

    /// <summary>
    ///     Ключи внешнего вида, которые навёл бы MapInit: компонент-владелец, ключ, заводское значение.
    /// </summary>
    private static readonly AppearanceDefault[] AppearanceDefaults =
    [
        // Слой «inserting» виден, пока ключа нет; OnMapInit ставит false, а нам MapInit не приходил.
        new("MaterialStorage", MaterialStorageVisuals.Inserting, false),
    ];

    /// <summary>
    ///     Запись таблицы дефолтов внешнего вида.
    /// </summary>
    /// <param name="Component">Yaml-имя компонента, по которому отбираем сущности.</param>
    /// <param name="Key">Ключ appearance-данных.</param>
    /// <param name="Value">Заводское значение, какое ставит MapInit.</param>
    private sealed record AppearanceDefault(string Component, Enum Key, object Value);

    /// <summary>
    ///     Прогоняет все проходы по состоянию переходящих процессов и пишет итог в лог одной строкой.
    /// </summary>
    /// <remarks>
    ///     Порядок важен: снятие маркеров само гасит «рабочий» режим, и следующий проход считает только то,
    ///     что маркера не имело, — например микроволновку, у которой <c>isWorking: true</c> приехал в файле.
    ///     Проходы проходят по всему серверу, как и остальные починки загрузки: сетки станция забирает к себе позже.
    /// </remarks>
    public void FixLoadedTransientStates()
    {
        var stripped = StripTransientComponents();
        var appeared = ApplyAppearanceDefaults();
        var idled = ResetWorkingStates();

        Log.Info($"SaveGamemode: переходящие состояния на загруженной карте: маркеров снято {stripped}, " +
                 $"ключей внешнего вида раздано {appeared}, машин возвращено в ожидание {idled}");
    }

    /// <summary>
    ///     Снимает маркерные компоненты переходящих процессов из <see cref="TransientComponents"/>.
    /// </summary>
    /// <returns>Сколько компонентов снято.</returns>
    public int StripTransientComponents()
    {
        var removed = 0;

        foreach (var name in TransientComponents)
        {
            if (!_componentFactory.TryGetRegistration(name, out var registration))
            {
                Log.Error($"SaveGamemode: компонент-маркер '{name}' не зарегистрирован — правило не сработает");
                continue;
            }

            // Сначала собираем, потом снимаем: снятие меняет состав сущностей прямо во время обхода.
            var doomed = new List<(EntityUid Uid, IComponent Comp)>();
            foreach (var (uid, comp) in _entityManager.GetAllComponents(registration.Type))
            {
                if (!TerminatingOrDeleted(uid))
                    doomed.Add((uid, comp));
            }

            foreach (var (uid, comp) in doomed)
            {
                // Снятие синхронное: откат побочного эффекта берёт на себя ComponentShutdown того же компонента.
                _entityManager.RemoveComponent(uid, comp);
                removed++;
            }
        }

        return removed;
    }

    /// <summary>
    ///     Раздаёт ключи внешнего вида из <see cref="AppearanceDefaults"/> — то, что поставил бы MapInit.
    /// </summary>
    /// <returns>Сколько ключей добавилось или изменилось.</returns>
    public int ApplyAppearanceDefaults()
    {
        var applied = 0;

        foreach (var entry in AppearanceDefaults)
        {
            if (!_componentFactory.TryGetRegistration(entry.Component, out var registration))
            {
                Log.Error($"SaveGamemode: компонента '{entry.Component}' нет — ключ '{entry.Key}' не раздан");
                continue;
            }

            foreach (var (uid, _) in _entityManager.GetAllComponents(registration.Type))
            {
                if (TerminatingOrDeleted(uid))
                    continue;

                // SetData молча пропускает сущность без AppearanceComponent и значение без изменения,
                // поэтому считаем сами: иначе лгали бы в логе о починках, которых не было.
                if (_appearance.TryGetData(uid, entry.Key, out var current) && Equals(current, entry.Value))
                    continue;

                _appearance.SetData(uid, entry.Key, entry.Value);
                applied++;
            }
        }

        return applied;
    }

    /// <summary>
    ///     Возвращает машины в холостой режим: <c>IsWorking</c> приехал из файла, а работы нет.
    /// </summary>
    /// <returns>Сколько машин переведено в ожидание.</returns>
    public int ResetWorkingStates()
    {
        var idled = 0;
        var query = AllEntityQuery<PowerStateComponent>();
        while (query.MoveNext(out var uid, out var powerState))
        {
            if (!powerState.IsWorking || TerminatingOrDeleted(uid))
                continue;

            // Присваиванием не правим: SetWorkingState пересчитывает нагрузку и поднимает PowerStateChanged.
            _powerState.TrySetWorkingState((uid, powerState), false);
            idled++;
        }

        return idled;
    }
}
