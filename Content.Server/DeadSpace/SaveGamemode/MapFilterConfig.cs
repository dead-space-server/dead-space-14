using System;
using System.Collections.Generic;

namespace Content.Server.DeadSpace.SaveGamemode;

/// <summary>
///     Способ обработки компонента, попавшего в фильтр сущностей.
/// </summary>
public enum EntityFilterAction
{
    /// <summary>
    ///     Удалить сущность целиком (её yaml uid добавится в множество удалённых,
    ///     ссылки на неё будут вычищены из остальных компонентов).
    /// </summary>
    Remove,

    /// <summary>
    ///     Оставить сущность, но удалить из неё перечисленные компоненты
    ///     (см. <see cref="StripComponentsByPrototype"/>).
    /// </summary>
    StripComponents,
}

/// <summary>
///     Конфигурация пост-процессора карты (<see cref="MapStateFilter"/>).
///     Описывает, какие сущности и какие поля компонентов попадают в итоговый дамп.
/// </summary>
/// <remarks>
///     Все списки имён компонентов работают по «yaml-имени» компонента — тому, что пишется
///     в поле <c>type:</c> внутри секции <c>components:</c> в YAML формата 7.
///     Имена сравниваются без учёта регистра.
/// </remarks>
public sealed class MapFilterConfig
{
    /// <summary>
    ///     Предикат, решающий судьбу сущности по её прототипу и списку компонентов.
    ///     Возвращает null — сущность сохраняется как есть.
    ///     Вызывается раньше остальных правил и имеет высший приоритет.
    /// </summary>
    public Func<string?, IReadOnlySet<string>, EntityFilterAction?>? EntityPredicate;

    /// <summary>
    ///     Id прототипов, чьи сущности удаляются целиком. Точное совпадение, без учёта регистра.
    /// </summary>
    public HashSet<string> RemovePrototypes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Маски id прототипов, чьи сущности удаляются целиком.
    ///     Поддерживаются <c>*</c> (любое число символов) и <c>?</c> (один символ), сравнение без учёта регистра.
    /// </summary>
    public List<string> RemovePrototypeMasks = new();

    /// <summary>
    ///     Компоненты, наличие которого у сущности приводит к её удалению целиком.
    ///     Работает по файлу, а в файл компонент попадает, только если отличается от прототипа:
    ///     для «прототипных» сущностей есть <see cref="RemovePrototypesWithComponents"/>.
    /// </summary>
    public HashSet<string> RemoveEntitiesWithComponents = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Компоненты, наличие которых в ПРОТОТИПЕ сущности приводит к её удалению целиком.
    ///     Пост-процессор прототипов не видит, поэтому правило раскрывает в
    ///     <see cref="RemovePrototypes"/> вызывающий код (<c>SaveSystem.CreateFilterConfig</c>).
    /// </summary>
    public HashSet<string> RemovePrototypesWithComponents = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Компоненты из <see cref="RemoveEntitiesWithComponents"/>, которые считаются признаком живности
    ///     только вместе с признаком существа из <see cref="CreatureMarkers"/>.
    ///     «Живое» поведение стоит и на технике (<c>HTN</c> есть у турелей) — без разделения
    ///     автономная механика вычищалась из сохранения вместе с мобами.
    /// </summary>
    public HashSet<string> DriverComponents = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Признак существа: сущность считается живностью, если хотя бы один из этих компонентов на ней есть.
    ///     <c>MobState</c> и <c>Body</c> нет ни у одной структуры или механизма.
    /// </summary>
    public HashSet<string> CreatureMarkers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Yaml uid сущностей, удаляемых явно (например, карта-«обёртка» или служебные сущности).
    /// </summary>
    public HashSet<int> RemoveUids = new();

    /// <summary>
    ///     Правила «оставить сущность, но снять компоненты»: имя компонента → набор удаляемых компонентов.
    ///     Ключ — id прототипа либо маска (<c>*</c>/<c>?</c>).
    /// </summary>
    public Dictionary<string, HashSet<string>> StripComponentsByPrototype = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Компоненты, удаляемые у всех сущностей независимо от прототипа.
    /// </summary>
    public HashSet<string> StripAllComponents = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Поля компонентов, удаляемые всегда — runtime-мусор, который должен взяться из прототипа
    ///     при загрузке (таймеры, адреса DeviceNetwork, кэши сигналов).
    ///     Имена пишутся как в YAML: у поля без явного тега первая буква строчная (<c>Delays</c> → <c>delays</c>),
    ///     регистр при сравнении не учитывается.
    /// </summary>
    public Dictionary<string, HashSet<string>> DropComponentFields = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Поля компонентов, удаляемые только если на сущности есть один из перечисленных компонентов.
    ///     Ключ — имя «условного» компонента, значение — набор (компонент → поля).
    /// </summary>
    public Dictionary<string, Dictionary<string, HashSet<string>>> DropFieldsWhenEntityHas =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Компоненты, которые никогда не чистятся правилами ниже <see cref="DropComponentFields"/>.
    ///     Нужны, чтобы «важное состояние» (например, баланс станции) гарантированно доехало до файла.
    /// </summary>
    public HashSet<string> ProtectedComponents = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Компоненты, которые снимаются со всех сущностей перед загрузкой карты и восстанавливаются
    ///     из sidecar-файла после неё (см. <see cref="MapStateFilter.BuildDeferredTree"/>).
    ///     Нужно для состояний, ломающих обычную map-загрузку или требующих уже готового мира.
    /// </summary>
    public HashSet<string> DeferredComponents = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Путь sidecar-файла с отложенными состояниями, относительно папки слота сохранения.
    /// </summary>
    public string DeferredFileName = "states.yml";

    /// <summary>
    ///     Заменять ссылки на удалённые сущности на <c>invalid</c> вместо удаления всего поля.
    ///     По умолчанию выключено: большинство компонентов корректно обрабатывает отсутствующее поле,
    ///     а явный <c>invalid</c> логируется движком как ошибка загрузки.
    /// </summary>
    public bool ReplaceDanglingReferencesWithInvalid;

    /// <summary>
    ///     Компоненты, в которых ссылки на удалённые сущности не вычищаются вообще.
    ///     Чистка опознаёт ссылки «по цифре», а у компонентов с ручными сериализаторами (декали,
    ///     атмосфера, плитки) в YAML лежат числовые ключи, совпадающие с uid по диапазону.
    /// </summary>
    public HashSet<string> ReferenceCleanExcludedComponents = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Имена полей, которые никогда не считаются ссылками на сущности — ни их значения, ни ключи внутри них.
    ///     Дополняют встроенный список <see cref="MapStateFilter"/>.
    /// </summary>
    public HashSet<string> ReferenceCleanExcludedFields = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Компоненты, которые снимаются с сущности, если их поле-ссылка ведёт в никуда.
    ///     Ключ — имя компонента в YAML, значение — имя поля, хранящего ссылку на сущность.
    ///     Поле типа <c>EntityUid</c> отсутствующим не бывает: читатель превращает его в несуществующую
    ///     сущность, и обработчик инициализации падает на её имени. Починить ссылку нечем — ставит её только рантайм.
    /// </summary>
    public Dictionary<string, string> ComponentsNeedingLiveLink = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Выравнивать ли флаг <c>mapInit</c>: проставить его сущностям и снять у самой карты.
    ///     Без флага у сущностей заново прогоняется MapInit и прототип возвращает компоненты наполнения,
    ///     которых в файле нет, — контейнеры набиваются вторым комплектом.
    ///     У карты флаг обязан быть снят: <c>GameTicker.StartRound</c> инициализирует <c>DefaultMap</c> сам.
    /// </summary>
    public bool NormalizeMapInitFlags = true;

    /// <summary>
    ///     Снимать ли компоненты, у которых нечем заполнить обязательные (<c>required</c>) поля:
    ///     иначе читатель падает на <c>RequiredFieldNotMappedException</c> и сохранение не загружается.
    ///     Работает только вместе с пробом полей (<see cref="IComponentFieldProbe"/>).
    /// </summary>
    public bool RepairRequiredComponents = true;

    /// <summary>
    ///     Удалять ли сущности без родителя, не числящиеся в корневых списках
    ///     (<c>maps</c>, <c>grids</c>, <c>orphans</c>, <c>nullspace</c>) — это содержимое вырезанных сущностей.
    ///     Иначе десериализатор падает на <c>EntityDeserializer.GetRootNodes</c> и карта не грузится целиком.
    ///     Если выключить, сироты дописываются в <c>orphans</c> и доезжают до карты.
    /// </summary>
    public bool RemoveEntitiesWithoutParent = true;

    /// <summary>
    ///     Конфигурация «режима по умолчанию»: карта сохраняется целиком, как она выглядит в конце раунда.
    ///     Из неё убираются только то, что картой не является (игроки и призраки), и runtime-мусор
    ///     внутри компонентов; состояние связи DeviceLink и балансы станции доезжают до файла.
    /// </summary>
    public static MapFilterConfig Default { get; } = CreateDefault();

    /// <summary>
    ///     Создаёт независимую копию конфигурации, чтобы вызывающий код мог спокойно её править.
    /// </summary>
    public MapFilterConfig Clone()
    {
        return new MapFilterConfig
        {
            EntityPredicate = EntityPredicate,
            RemovePrototypes = new HashSet<string>(RemovePrototypes, StringComparer.OrdinalIgnoreCase),
            RemovePrototypeMasks = new List<string>(RemovePrototypeMasks),
            RemoveEntitiesWithComponents = new HashSet<string>(RemoveEntitiesWithComponents, StringComparer.OrdinalIgnoreCase),
            RemovePrototypesWithComponents = new HashSet<string>(RemovePrototypesWithComponents, StringComparer.OrdinalIgnoreCase),
            DriverComponents = new HashSet<string>(DriverComponents, StringComparer.OrdinalIgnoreCase),
            CreatureMarkers = new HashSet<string>(CreatureMarkers, StringComparer.OrdinalIgnoreCase),
            RemoveUids = new HashSet<int>(RemoveUids),
            StripComponentsByPrototype = CloneNested(StripComponentsByPrototype),
            StripAllComponents = new HashSet<string>(StripAllComponents, StringComparer.OrdinalIgnoreCase),
            DropComponentFields = CloneNested(DropComponentFields),
            DropFieldsWhenEntityHas = CloneDeepNested(DropFieldsWhenEntityHas),
            ProtectedComponents = new HashSet<string>(ProtectedComponents, StringComparer.OrdinalIgnoreCase),
            DeferredComponents = new HashSet<string>(DeferredComponents, StringComparer.OrdinalIgnoreCase),
            DeferredFileName = DeferredFileName,
            ReplaceDanglingReferencesWithInvalid = ReplaceDanglingReferencesWithInvalid,
            ComponentsNeedingLiveLink = new Dictionary<string, string>(ComponentsNeedingLiveLink, StringComparer.OrdinalIgnoreCase),
            NormalizeMapInitFlags = NormalizeMapInitFlags,
            RepairRequiredComponents = RepairRequiredComponents,
            RemoveEntitiesWithoutParent = RemoveEntitiesWithoutParent,
            ReferenceCleanExcludedComponents = new HashSet<string>(ReferenceCleanExcludedComponents, StringComparer.OrdinalIgnoreCase),
            ReferenceCleanExcludedFields = new HashSet<string>(ReferenceCleanExcludedFields, StringComparer.OrdinalIgnoreCase),
        };
    }

    private static Dictionary<string, HashSet<string>> CloneNested(Dictionary<string, HashSet<string>> source)
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in source)
        {
            result[key] = new HashSet<string>(value, StringComparer.OrdinalIgnoreCase);
        }

        return result;
    }

    private static Dictionary<string, Dictionary<string, HashSet<string>>> CloneDeepNested(
        Dictionary<string, Dictionary<string, HashSet<string>>> source)
    {
        var result = new Dictionary<string, Dictionary<string, HashSet<string>>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in source)
        {
            result[key] = CloneNested(value);
        }

        return result;
    }

    /// <summary>
    ///     Конфигурация по умолчанию для режима сохранения раунда.
    ///     Имена компонентов и полей сверены с исходниками: имя в YAML — имя класса без суффикса
    ///     <c>Component</c> и префиксов <c>Client</c>/<c>Server</c>/<c>Shared</c>, имя поля — строка
    ///     из <c>[DataField]</c> либо имя C#-поля с первой строчной буквой.
    /// </summary>
    public static MapFilterConfig CreateDefault()
    {
        var config = new MapFilterConfig();

        // --- Сущности: картой не являются только игроки и призраки. ---
        config.RemoveEntitiesWithComponents.UnionWith([
            "Ghost", // Content.Shared.Ghost.GhostComponent
            "Actor", // Robust.Shared.Player.ActorComponent — игрок, он не часть карты
        ]);

        // --- Сущности действий: их в сохранении быть не должно. ---
        // Действие — отдельная сущность, ребёнок существа в контейнере «actions». При загрузке она
        // создаётся заново: MobStateActionsSystem.ComposeActions выдаёт действия состояний на ComponentInit,
        // боевой режим и крик — на MapInit. Сохранённое действие — как минимум лишняя сущность,
        // как максимум висячая ссылка в состоянии для клиентов.
        //
        // Имена точные, не по маске: «Actions» и «ActionsContainer» стоят на самом существе, удалять по ним
        // означало бы вычистить из сохранения мобов, а существа — часть мира.
        config.RemoveEntitiesWithComponents.UnionWith([
            "Action", // Content.Shared.Actions.Components.ActionComponent
            "InstantAction",
            "TargetAction",
            "EntityTargetAction",
            "WorldTargetAction",
        ]);

        // Файл — плохой свидетель: действие со всеми прототипными полями не выдало бы ни одного
        // «action»-имени. Кто является действием, знает только прототип.
        config.RemovePrototypesWithComponents.UnionWith([
            "Action",
            "InstantAction",
            "TargetAction",
            "EntityTargetAction",
            "WorldTargetAction",
        ]);

        // --- Компоненты: снимаем со всех сущностей. ---
        // Снятие не удаляет компонент при загрузке — движок доберёт его из прототипа; убирается именно
        // runtime-состояние внутри, поэтому списки ниже дублируются в DropComponentFields.
        //
        // DeviceLink здесь отсутствует намеренно: связи «источник → сток» живут только в
        // DeviceLinkSource.LinkedPorts (LinkedSources у стока и Outputs не сериализуются), их восстанавливает
        // SharedDeviceLinkSystem.OnSourceStartup. Снять источник или выкинуть LinkedPorts — значит потерять
        // проводку безвозвратно, причём визуально она останется целой, а работать перестанет только сигнал.
        // LastSignals — тоже состояние: по нему источник догоняет сток, подключённый после загрузки.
        config.StripAllComponents.UnionWith([
            "UseDelay",
            "DeviceNetwork",
            "DeviceList",
            "Ringer",
            // Пустой узел «ActionsContainer» меняет ПОРЯДОК инициализации сущности, и на загрузке это
            // роняло карту. Компоненты из файла инициализируются позже прототипных, а в прототипе моба
            // этого контейнера нет — его навешивает рантайм (ActionContainerSystem.EnsureComp). Пришедший
            // из файла компонент ещё в стадии Added, его OnInit не вызывался, контейнер не создан —
            // NullReferenceException в SharedContainerSystem.Insert из ComposeActions.
            // Данных в узле нет, прототип их не теряет: снимаем и загрузка повторяет обычный спавн.
            "ActionsContainer",
        ]);

        // --- Поля: runtime-состояние, которое должно взяться из прототипа при загрузке. ---
        AddDropped(config, "UseDelay", "delays");
        AddDropped(config, "DeviceNetwork", "address", "deviceLists", "configurators");
        AddDropped(config, "DeviceList", "devices", "configurators");
        AddDropped(config, "Ringer", "nextNoteTime", "nextRingtoneSetTime");
        // Счётчик троттлинга вызовов стока отсчитывается от InvokeCounterTick, а то поле не сериализуется:
        // после загрузки тик считается нулём, и сток, у которого счётчик упирался в InvokeLimit, грузился
        // уже «перегруженным» и при первом сигнале сбрасывал связи через DeviceLinkOverloadEvent.
        AddDropped(config, "DeviceLinkSink", "invokeCounter");

        // --- Существа: ссылки на сущности действий, которых в файле больше нет. ---
        // Отсутствие поля здесь то же, что «пусто»: поля типизированы как EntityUid?, а наполняет их рантайм
        // (ComposeActions на ComponentInit, боевой режим и крик на MapInit) — существо получает тот же набор
        // действий, что и при обычном спавне. Наполнение контейнера «actions» не прописано: ссылки внутри
        // вырезает типизированная чистка (ключи «ents»/«ent» описаны в EntityFieldWalker).
        AddDropped(config, "Actions", "actions");
        AddDropped(config, "MobStateActions", "grantedActions");
        AddDropped(config, "CombatMode", "combatToggleActionEntity");
        AddDropped(config, "Vocal", "screamActionEntity");

        // --- Важное состояние: его не трогаем даже глобальными правилами. ---
        config.ProtectedComponents.UnionWith([
            "StationBankAccount", // Content.Shared.Cargo.StationBankAccountComponent.Accounts — баланс станции
        ]);

        // --- Структурные компоненты сетки: ссылок на сущности в них нет вовсе. ---
        // Числа в них (id декалей и чанков, индексы тайлов, слои труб) по диапазону совпадают с yaml uid,
        // и «чистка ссылок по числу» съедала у DecalGrid ключ `nodes` — падение при загрузке.
        config.ReferenceCleanExcludedComponents.UnionWith([
            "DecalGrid",
            "GridAtmosphere",
            "TileMap",
            "TileHistory",
            "MapGrid",
            "GridTree",
            "OccluderTree",
            "GasTileOverlay",
            "AtmosPipeLayers",
            "AtmosPipeColor",
            "AtmosMonitor",
            "RadiationGridResistance",
            "TileFrictionModifier",
            "ExplosionAirtightGrid",
            "SpreaderGrid",
            "StarGrid",
            "GridPathfinding",
        ]);

        config.ReferenceCleanExcludedFields.UnionWith([
            "chunks",
            "indirect",
            "nodes",
            "decals",
            "version",
            "data",
        ]);

        // --- Компоненты со ссылкой, которую нечем починить: снимаем их целиком. ---
        // StationMember.Station — не nullable EntityUid, связь ставит только StationSystem.AddGridToStation.
        // Станция-сущность к моменту сохранения уже удалена, и при загрузке PdaSystem.UpdateStationName
        // зовёт Name(0): KeyNotFoundException роняет загрузку всей карты.
        config.ComponentsNeedingLiveLink["StationMember"] = "station";

        return config;
    }

    private static void AddDropped(MapFilterConfig config, string component, params string[] fields)
    {
        // Регистр не учитываем: в YAML у поля первая буква строчная, а правило можно написать и именем C#-поля.
        if (!config.DropComponentFields.TryGetValue(component, out var set))
            config.DropComponentFields[component] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in fields)
        {
            set.Add(field);
        }
    }
}
