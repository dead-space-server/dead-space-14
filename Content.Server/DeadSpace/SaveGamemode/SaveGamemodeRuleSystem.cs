using System;
using System.Collections.Generic;
using System.IO;
using Content.Server.Cargo.Systems;
using Content.Server.DeviceNetwork.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Maps;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Maps;
using Content.Shared.Paper;
using Content.Shared.Station.Components;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.DeadSpace.SaveGamemode;

/// <summary>
///     Игровое правило режима сохранения/восстановления раунда:
///     при старте подменяет стандартную карту сохранением, при конце пишет карту обратно.
/// </summary>
public sealed class SaveGamemodeRuleSystem : GameRuleSystem<SaveGamemodeRuleComponent>
{
    [Dependency] private readonly SaveSystem _save = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly IResourceManager _res = default!;
    [Dependency] private readonly IGameMapManager _gameMap = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly DeviceNetworkSystem _deviceNet = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly CargoSystem _cargo = default!;

    /// <summary>
    ///     Имя, которое движок даёт сущности сетки при создании. Для сетки из сохранения оно означает
    ///     «имя станции не записано», см. <see cref="IsStationName"/>.
    /// </summary>
    private const string DefaultGridName = "grid";

    /// <summary>
    ///     Результат проверки файла сохранения за текущий раунд.
    ///     Проверка читает только начало файла, но файл режимного сохранения большой и открывается
    ///     он дважды за раунд не зря: <see cref="OnLoadingMaps"/> выполняется раньше
    ///     <see cref="Started"/> и всегда обновляет кэш.
    /// </summary>
    private readonly Dictionary<string, bool> _saveValidity = new();

    /// <summary>
    ///     Карты, которые режим отменил ради сохранения (первая — будущая DefaultMap).
    ///     Нужны, чтобы при неудачной загрузке сохранения вернуть раунду обычную станцию:
    ///     пустая DefaultMap без станции заканчивается падением на распределении работ.
    /// </summary>
    private readonly List<GameMapPrototype> _replacedMaps = new();

    /// <summary>
    ///     Идёт регистрация станции для карты, поднятой из сохранения.
    ///     В это окно обязательные гриды раунда станции не спавнятся, см. <see cref="OnStationGridAdded"/>.
    /// </summary>
    private bool _registeringSavedMap;

    /// <summary>
    ///     Станции, у которых спавн обязательных гридов уже убран — чтобы не писать об этом в лог на каждую сетку.
    /// </summary>
    private readonly HashSet<EntityUid> _gridsSuppressed = new();

    /// <summary>
    ///     Карта, поднятая из сохранения в этом раунде, и её состояния станций.
    ///     Нужны, чтобы в начале раунда ещё раз пройтись по станциям: всё, что сделано при загрузке
    ///     карты, — самое хрупкое место раунда, и потерянное там имя потом не вернуть.
    /// </summary>
    private MapId _savedMap = MapId.Nullspace;

    private List<SavedStationState> _savedStates = new();

    /// <summary>
    ///     Имена, подставленные в документы вместо сохранённого имени станции: выдуманное имя -> сохранённое.
    ///     См. <see cref="FixStationNamesInDocuments"/>.
    /// </summary>
    private readonly Dictionary<string, string> _documentNames = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LoadingMapsEvent>(OnLoadingMaps, before: [typeof(StationSystem)]);
        SubscribeLocalEvent<StationGridAddedEvent>(OnStationGridAdded);
        SubscribeLocalEvent<RoundEndedEvent>(OnRoundEnded);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
    }

    /// <summary>
    ///     Начало раунда — последний момент, когда имя станции ещё ничего не успело о себе закрепить.
    /// </summary>
    /// <remarks>
    ///     Регистрация станции идёт при загрузке карты, и всё, что там не сложилось (бросок в одном из
    ///     шагов, станция без состояния), в середине раунда уже не починить: напечатанные документы
    ///     несут имя в тексте, а не ссылкой на станцию. Здесь имена сверяются заново — бесплатно,
    ///     станций на карте немного, а документы после этого печатаются только с верным именем.
    /// </remarks>
    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        // Новый раунд — старое состояние прошлого раунда больше не действует.
        if (ev.New == GameRunLevel.PreRoundLobby)
        {
            _savedMap = MapId.Nullspace;
            _savedStates = new List<SavedStationState>();
            _documentNames.Clear();
            return;
        }

        if (ev.New != GameRunLevel.InRound || _savedMap == MapId.Nullspace)
            return;

        TryStep("списков сеток станций", PruneDeadStationGrids);

        // Генератор имён снимается здесь, а не при загрузке: MapInit карты идёт ПОСЛЕ регистрации станции
        // (GameTicker.StartRound: StartGamePresetRules -> InitializeMap(DefaultMap) -> SpawnPlayers),
        // и на этом MapInit станция переименовывается заново. Снятый раньше MapInit компонент движок
        // вернуть не может, а в момент загрузки его ComponentInit уже прошёл — снимать было нечего.
        TryStep("генератора имён", SilenceStationNameGeneration);

        var byId = StatesById(_savedStates);

        var query = AllEntityQuery<StationDataComponent>();
        while (query.MoveNext(out var station, out var data))
        {
            SavedStationState? state = null;
            var grids = new List<EntityUid>();

            foreach (var grid in data.Grids)
            {
                if (!TryComp(grid, out TransformComponent? xform) || xform.MapID != _savedMap)
                    continue;

                grids.Add(grid);

                if (state != null)
                    continue;

                if (TryComp<BecomesStationComponent>(grid, out var becomes) &&
                    !string.IsNullOrEmpty(becomes.Id) &&
                    byId.TryGetValue(becomes.Id, out var found))
                {
                    state = found;
                }
            }

            if (state == null || !IsStationName(state.Name))
                continue;

            var current = Name(station);
            if (current == state.Name)
                continue;

            _station.RenameStation(station, state.Name, false, data);
            _documentNames[current] = state.Name;

            foreach (var grid in grids)
            {
                if (MetaData(grid).EntityName == current)
                    _metaData.SetEntityName(grid, state.Name);
            }

            Log.Info($"SaveGamemode: в начале раунда станции {ToPrettyString(station)} возвращено " +
                     $"имя из сохранения: '{current}' -> '{state.Name}'");
        }

        FixStationNamesInDocuments();
    }

    /// <summary>
    ///     <c>BecomesStationComponent.Id</c> сетки станции -> её состояние из <c>station.yml</c>.
    /// </summary>
    private static Dictionary<string, SavedStationState> StatesById(IReadOnlyList<SavedStationState> states)
    {
        var byId = new Dictionary<string, SavedStationState>(StringComparer.OrdinalIgnoreCase);

        foreach (var state in states)
        {
            foreach (var id in state.Ids)
            {
                byId.TryAdd(id, state);
            }
        }

        return byId;
    }

    /// <summary>
    ///     Выключает генератор имён станций на загруженной карте.
    /// </summary>
    /// <remarks>
    ///     Порядок старта раунда в <c>GameTicker.StartRound</c>: загрузка карты, затем
    ///     <c>_map.InitializeMap(DefaultMap)</c>, и только потом спавн игроков и <c>RoundStartedEvent</c>.
    ///     Станция при загрузке создаётся заново, поэтому в её прототипе остаётся
    ///     <c>StationNameSetupComponent</c>, который на этом MapInit вызывает <c>RenameStation</c> с новым
    ///     выдуманным именем — то есть портит имя из сохранения ровно в ту секунду, когда его уже прочитали
    ///     сетки, PDA и рассылка работ, но до того, как по нему начнут печатать документы.
    ///     Сам компонент выключается, а не переименование отменяется: станция при загрузке создаётся
    ///     тикером, и в момент регистрации станции её <c>ComponentInit</c> уже прошёл — ловить там было нечего.
    ///     Снимается компонент со станции целиком, без него станция живёт: имя генерируется только им.
    ///     Место вызова — <c>InRound</c>, а не загрузка карты: между ними и есть тот самый MapInit,
    ///     который переименовывает станцию. Снятый до него компонент движок вернуть не сможет.
    /// </remarks>
    private void SilenceStationNameGeneration()
    {
        if (_savedMap == MapId.Nullspace)
            return;

        var removed = 0;
        var query = AllEntityQuery<StationDataComponent>();
        while (query.MoveNext(out var station, out var data))
        {
            var onSavedMap = false;
            foreach (var grid in data.Grids)
            {
                if (TryComp(grid, out TransformComponent? xform) && xform.MapID == _savedMap)
                {
                    onSavedMap = true;
                    break;
                }
            }

            if (!onSavedMap || !RemComp<StationNameSetupComponent>(station))
                continue;

            removed++;
        }

        if (removed > 0)
        {
            Log.Info($"SaveGamemode: у станций загруженной карты выключен генератор имён " +
                     $"(станций: {removed}) — иначе MapInit переименует их после загрузки");
        }
    }

    /// <summary>
    ///     Убирает из списка сеток станции те, которых больше нет.
    /// </summary>
    /// <remarks>
    ///     <c>StationDataComponent.Grids</c> — <c>[AutoNetworkedField]</c>: каждое состояние станции уезжает
    ///     на клиент списком идентификаторов сеток. Живая сетка уходит из списка сама, когда у неё снимается
    ///     <c>StationMemberComponent</c> (<c>StationSystem.OnStationGridDeleted</c>). Удалённая — нет: её
    ///     компонент уже снят и снимать нечего, а ссылка на сетку в списке осталась.
    ///     Сохранение это только усиливает: карта целиком пересоздаётся, старые сетки удаляются пачкой —
    ///     из них и набираются мёртвые ссылки. У такой сетки <c>MetaData</c> ещё живёт до конца тика,
    ///     поэтому её net id спокойно попадает в состояние станции и уходит клиентам.
    ///     На клиенте такой сетки нет: состояние приходит на отсутствующую сущность, и в лог идут
    ///     «Encountered deleted entity while merging implicit data» и «Can't resolve MetaDataComponent
    ///     on entity ...».
    ///     Время — начало раунда: раньше нельзя, пока регистрация станции не закончена, позже поздно —
    ///     состояние с мёртвой ссылкой уже ушло клиентам.
    ///     Живая сетка вычеркивается через <c>RemoveGridFromStation</c>: доступ к списку сеток у чужой
    ///     системы только на чтение (RA0002), а этот метод ещё и поднимает <c>StationGridRemovedEvent</c> —
    ///     как при обычном выходе сетки из станции. Удалённая вычеркивается снятием связи, потому что
    ///     <c>RemoveGridFromStation</c> на ней не работает: у удалённой сущности нет <c>MapGridComponent</c>.
    /// </remarks>
    private void PruneDeadStationGrids()
    {
        if (_savedMap == MapId.Nullspace)
            return;

        var removed = 0;
        var query = AllEntityQuery<StationDataComponent>();
        while (query.MoveNext(out var station, out var data))
        {
            // HashSet менять во время итерации нельзя, поэтому сначала только смотрим: есть ли мёртвые
            // сетки и принадлежит ли станция загруженной карте.
            var dead = 0;
            var onSavedMap = false;

            foreach (var grid in data.Grids)
            {
                if (!Exists(grid))
                {
                    dead++;
                    continue;
                }

                if (!onSavedMap && TryComp(grid, out TransformComponent? xform) && xform.MapID == _savedMap)
                    onSavedMap = true;
            }

            // Чужие станции (шаттл-карты, хаб) не трогаем: у них свои сетки и своя жизнь.
            if (dead == 0 || !onSavedMap)
                continue;

            var doomed = new List<EntityUid>();
            foreach (var grid in data.Grids)
            {
                if (!Exists(grid))
                    doomed.Add(grid);
            }

            // Удалённая сетка вычёркивается снятием связи со станции: у неё уже нет MapGridComponent,
            // поэтому RemoveGridFromStation её не возьмёт (требует его через Resolve), а обычный путь —
            // ComponentShutdown у StationMemberComponent — прошёл ещё при удалении сетки и второй раз
            // не случится. Снятие того же компонента сейчас вычеркнет ссылку сам, через
            // OnStationGridDeleted. Доступ к самому списку сеток у чужой системы только на чтение,
            // поэтому для живой сетки берётся RemoveGridFromStation.
            foreach (var grid in doomed)
            {
                if (!Exists(grid))
                {
                    RemComp<StationMemberComponent>(grid);
                }
                else
                {
                    _station.RemoveGridFromStation(station, grid, stationData: data);
                }

                removed++;
            }
        }

        if (removed > 0)
        {
            Log.Info($"SaveGamemode: из списков сеток станций убрано удалённых сеток: {removed} — " +
                     "иначе их net id уходит клиентам и ломает применение состояния");
        }
    }

    /// <summary>
    ///     Шаг восстановления станции, который не должен отменять остальные.
    /// </summary>
    /// <remarks>
    ///     Имя станции, её балансы и её сетки — самостоятельные вещи: упавшие балансы не имеют права
    ///     оставлять станцию с выдуманным именем. Прежде один бросок после <c>PostGameMapLoad</c>
    ///     обрывал всё, что шло за ним, и раунд жил со станцией, переименованной заново.
    /// </remarks>
    private void TryStep(string what, Action step)
    {
        try
        {
            step();
        }
        catch (Exception e)
        {
            Log.Error($"SaveGamemode: не удалось применить {what}:\n{e}");
        }
    }

    /// <summary>
    ///     Снимает у станции, созданной для загруженного сохранения, компоненты, которые на
    ///     <c>StationPostInitEvent</c> заводят обязательные гриды раунда.
    /// </summary>
    /// <remarks>
    ///     Станцию создаёт <c>InitializeNewStation</c> внутри <c>PostGameMapLoad</c>, и сразу после этого
    ///     <c>StationPostInitEvent</c> рождает карго-шаттл, торговую станцию, шаттл утилизаторов и руины.
    ///     Сохранение уже содержит их прошлые версии, поэтому раунд получал по два комплекта.
    ///     <c>StationGridAddedEvent</c> — единственная точка между появлением станции и её пост-инициализацией.
    ///     Спасательный и прибывающий шаттлы не трогаем: их гриды на отдельных картах, в сохранение они не
    ///     попадают, а прибытие нужно для позднего захода игроков.
    /// </remarks>
    private void OnStationGridAdded(StationGridAddedEvent ev)
    {
        if (!_registeringSavedMap)
            return;

        // ИЛИ, а не И: снимаются оба компонента, независимо от того, какой из них оказался на станции.
        var removed = RemComp<GridSpawnComponent>(ev.Station) | RemComp<StationCargoShuttleComponent>(ev.Station);

        if (removed && _gridsSuppressed.Add(ev.Station))
        {
            Log.Info($"SaveGamemode: станция {ToPrettyString(ev.Station)} из сохранения — обязательные гриды " +
                     "(карго-шаттл, торговая станция, шаттл утилизаторов, руины) не спавнятся");
        }
    }

    /// <summary>
    ///     Перехватывает загрузку стандартных карт: если режим грузится из сохранения, очищаем список карт —
    ///     движок создаст пустую DefaultMap, а в Started мы подменим её сохранением.
    ///     Пустой или битый файл карту не отменяет, иначе раунд стартовал бы без станции.
    /// </summary>
    private void OnLoadingMaps(LoadingMapsEvent ev)
    {
        var query = QueryAllRules();
        while (query.MoveNext(out var uid, out var comp, out _))
        {
            if (!comp.LoadOnStart)
                continue;

            var path = SaveSystem.GetSavePath(comp.SaveSlot);
            var valid = _save.SlotExists(comp.SaveSlot) && IsSaveFileValid(path);
            _saveValidity[comp.SaveSlot] = valid;

            if (!valid)
            {
                Log.Error($"SaveGamemode: сохранение '{comp.SaveSlot}' пусто или повреждено ({path}), " +
                          "берётся стандартная карта");
                continue;
            }

            Log.Info($"SaveGamemode: подмена стандартной карты сохранением '{comp.SaveSlot}'");
            // Запоминаем отменённые карты: если сохранение не загрузится, раунду нужна обычная станция.
            _replacedMaps.Clear();
            _replacedMaps.AddRange(ev.Maps);
            ev.Maps.Clear();
            break;
        }
    }

    protected override void Started(EntityUid uid, SaveGamemodeRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        Log.Info($"SaveGamemode rule started: {uid}");

        if (component.LoadOnStart && _save.SlotExists(component.SaveSlot))
        {
            var path = SaveSystem.GetSavePath(component.SaveSlot);
            // OnLoadingMaps уже проверил файл в этом раунде; если правила запускались вне стандартного
            // старта раунда, проверяем на месте.
            var valid = _saveValidity.TryGetValue(component.SaveSlot, out var cached)
                ? cached
                : IsSaveFileValid(path);

            if (valid)
            {
                TryLoadSaveAsDefaultMap(component);
            }
            else
            {
                Log.Error($"SaveGamemode: сохранение '{component.SaveSlot}' повреждено или пустое ({path}), загрузка отменена. Будет использована стандартная карта.");
            }
        }

        // TODO: логика старта режима (спавн игроков, телепортация на карту и т.д.)
    }

    /// <summary>
    ///     Проверяет, что файл сохранения существует и начинается с YAML-документа-маппинга.
    /// </summary>
    /// <remarks>
    ///     Проверка обязана быть дешёвой: файл режимного сохранения — сотни мегабайт.
    ///     Прежний вариант читал файл целиком в строку (<c>ReadToEnd</c>), а потом строил из неё
    ///     полное дерево <c>YamlStream</c>, чтобы взять из него одно число — сколько документов.
    ///     Итого два полных разбора большого файла до того, как карту начнёт грузить загрузчик.
    ///     Здесь поток событий YamlDotNet: разбирается ровно начало файла, остальное не читается
    ///     и в памяти не держится. Глубокий обрыв или битый YAML в середине файла увидит
    ///     <see cref="MapLoaderSystem"/> при загрузке — для этого есть путь с починкой файла.
    ///     Корректный файл карты всегда начинается с маппинга (<c>meta</c>, <c>maps</c>,
    ///     <c>entities</c>), поэтому скаляр или последовательность на месте корня — явный мусор.
    /// </remarks>
    private bool IsSaveFileValid(ResPath path)
    {
        var rooted = path.ToRootedPath();
        if (!_res.UserData.Exists(rooted))
            return false;

        try
        {
            using var reader = _res.UserData.OpenText(rooted);
            var parser = new YamlDotNet.Core.Parser(reader);

            // События выдаются по одному, поэтому читаем ровно столько, сколько разобрали.
            // Пустой файл и файл из одних комментариев/разделителей до документа не доходит.
            while (parser.Current is not YamlDotNet.Core.Events.StreamEnd
                   and not YamlDotNet.Core.Events.DocumentStart)
            {
                if (!parser.MoveNext())
                    return false;
            }

            // Документ открылся и тут же кончился — файл обрезан или битый.
            return parser.Current is YamlDotNet.Core.Events.DocumentStart
                   && parser.MoveNext()
                   && parser.Current is YamlDotNet.Core.Events.MappingStart;
        }
        catch (Exception e)
        {
            Log.Error($"SaveGamemode: ошибка при проверке сохранения {path}: {e.Message}");
            return false;
        }
    }

    protected override void Ended(EntityUid uid, SaveGamemodeRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);
        Log.Info($"SaveGamemode rule ended: {uid}");

        // TODO: очистка/завершение режима
    }

    /// <summary>
    ///     При завершении раунда: сохраняем карту, удаляем временные файлы,
    ///     автоматически переключаем пресет на следующий.
    /// </summary>
    private void OnRoundEnded(RoundEndedEvent ev)
    {
        // Кэш проверки файла относится к завершившемуся раунду: впереди новые сохранения и новая загрузка.
        _saveValidity.Clear();
        _replacedMaps.Clear();

        var query = QueryAllRules();
        while (query.MoveNext(out var uid, out var comp, out _))
        {
            if (comp.SaveOnRoundEnd)
                TrySaveCurrentMap(comp);

            if (comp.DeleteSaveOnRoundEnd)
                _save.TryDeleteSlot(comp.SaveSlot);

            if (!string.IsNullOrWhiteSpace(comp.NextPreset))
            {
                Log.Info($"SaveGamemode: переключение пресета на '{comp.NextPreset}'");
                GameTicker.SetGamePreset(comp.NextPreset, force: false);
            }

            break;
        }
    }

    private void TryLoadSaveAsDefaultMap(SaveGamemodeRuleComponent component)
    {
        var defaultMap = GameTicker.DefaultMap;
        if (defaultMap == MapId.Nullspace)
        {
            Log.Error("SaveGamemode: DefaultMap не установлен, загрузка сохранения невозможна");
            return;
        }

        var path = SaveSystem.GetSavePath(component.SaveSlot);

        // Файл мог быть записан версией, срезавшей mapInit: без флага движок заново прогоняет
        // MapInit, и наполнение контейнеров из прототипа высыпается вторым комплектом.
        _save.PrepareMapInitFlags(path);

        var loaded = TryLoadOnce(defaultMap, path, out var map);

        if (!loaded || map == null)
        {
            // Файл может быть синтаксически целым, но нечитаемым по составу: компонент без обязательного
            // поля, которое нечем заполнить. Пост-процессор умеет такое вычищать, а заодно сохраняет
            // предыдущую версию файла в .bak — пробуем загрузиться после него.
            Log.Warning($"SaveGamemode: сохранение '{component.SaveSlot}' не загрузилось, " +
                        $"пробую починить файл и загрузить снова");

            if (!CanReuseMapSlot(defaultMap))
            {
                // Загрузка оборвалась на середине, и карта осталась в состоянии удаления.
                // Повторно загрузиться на этот MapId уже нельзя, поэтому не тратим минуту на
                // пост-обработку файла, а сразу уходим на запасную карту.
                Log.Error($"SaveGamemode: карта {defaultMap} не освобождена, повторная загрузка невозможна");
            }
            else if (_save.TryFilterSaveFile(path, out var repair))
            {
                Log.Info($"SaveGamemode: файл '{component.SaveSlot}' пропущен через пост-обработку. {repair}");
                loaded = TryLoadOnce(defaultMap, path, out map);
            }
        }

        if (!loaded || map == null)
        {
            Log.Error($"SaveGamemode: не удалось загрузить сохранение '{component.SaveSlot}' из {path}");

            // Пустую DefaultMap мы уже удалили, а без карты раунд существовать не может:
            // возвращаем карту с тем же MapId, иначе спавн игроков упадёт на несуществующей карте.
            // Если загрузка оборвалась на середине, на этом MapId могла остаться недосозданная
            // карта — её выбрасываем и собираем обычную станцию.
            if (!TryRestoreReplacedMaps(defaultMap))
                TryCreateEmptyMap(defaultMap);

            return;
        }

        Log.Info($"SaveGamemode: сохранение '{component.SaveSlot}' загружено на карту {defaultMap}");

        WakeLoadedNpcs(defaultMap);

        // Сама станция в файл не сериализуется: она живёт в nullspace, и на карте остаётся только
        // пометка BecomesStation у сетки. Без станции раунд обрывается на спавне игроков, поэтому
        // регистрируем её так же, как это делает тикер для обычной карты.
        if (TryRegisterStations(defaultMap, component.SuppressRoundstartGrids, component.SaveSlot))
        {
            ConnectLoadedDevices(defaultMap);
            return;
        }

        Log.Error("SaveGamemode: станцию зарегистрировать не удалось, раунд уходит на обычную карту");

        if (!TryRestoreReplacedMaps(defaultMap))
            TryCreateEmptyMap(defaultMap);
    }

    /// <summary>
    ///     Регистрирует станции на карте, поднятой из сохранения.
    /// </summary>
    /// <remarks>
    ///     Станция не часть карты: <c>StationData</c>/<c>StationJobs</c> создаёт <c>InitializeNewStation</c>
    ///     во время раунда, в файле от неё остаётся только <c>BecomesStationComponent</c> у сетки. Без станции
    ///     <c>SpawnPlayers</c> падает на <c>Assert(stations.Count > 0)</c>. Поднимаем то же событие
    ///     <c>PostGameMapLoad</c>, что и тикер для обычной карты, в то же окно между загрузкой и спавном.
    ///     Состояние станции (работы, банк) в файле карты не лежит — станция вне карты. Её имя и балансы
    ///     режим пишет рядом со слотом в <c>station.yml</c> и применяет отсюда:
    ///     см. <see cref="ApplyStationStates"/>. Файла нет (старое сохранение) — имя хотя бы берётся
    ///     с сетки, см. <see cref="RestoreStationNames"/>, а балансы остаются заводскими.
    /// </remarks>
    /// <param name="suppressRoundstartGrids">
    ///     Не спавнять обязательные гриды станции на время регистрации: в сохранении они уже есть.
    /// </param>
    /// <param name="slot">Слот сохранения — из него читается состояние станций.</param>
    /// <returns>
    ///     False, если на карте нет сетки со станцией или для неё не нашлось конфигурации:
    ///     раунд на такой карте существовать не может.
    /// </returns>
    private bool TryRegisterStations(MapId mapId, bool suppressRoundstartGrids, string slot)
    {
        var grids = new List<EntityUid>();
        var stationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Имена станций, записанные в сохранении: сетка -> имя. Собираются здесь, применяются
        // после регистрации, см. <see cref="RestoreStationNames"/>.
        var savedNames = new Dictionary<EntityUid, string>();
        var alreadyRegistered = 0;

        // Главная сетка карты — самая большая: это сама станция, а не капсулы, шаттлы и точки интереса.
        // Именно её id должен получить конфигурацию, иначе станция соберётся из капсул.
        EntityUid? primaryGrid = null;
        var primaryTiles = 0;
        var primaryId = (string?)null;

        var query = AllEntityQuery<MapGridComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var gridComp, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            // Сетка уже приписана к станции — повторная регистрация ей не нужна.
            if (HasComp<StationMemberComponent>(uid))
            {
                alreadyRegistered++;
                continue;
            }

            grids.Add(uid);

            if (TryComp<BecomesStationComponent>(uid, out var becomes) && !string.IsNullOrEmpty(becomes.Id))
            {
                stationIds.Add(becomes.Id);

                var tiles = (int)(gridComp.LocalAABB.Width * gridComp.LocalAABB.Height);
                if (primaryGrid == null || tiles > primaryTiles)
                {
                    primaryGrid = uid;
                    primaryTiles = tiles;
                    primaryId = becomes.Id;
                }

                // Имя станции остаётся подписанным на её сетке: MetaData есть у каждой сущности.
                var gridName = MetaData(uid).EntityName;
                if (IsStationName(gridName))
                    savedNames[uid] = gridName;
            }
        }

        if (stationIds.Count == 0)
        {
            if (alreadyRegistered > 0)
                return true;

            Log.Error($"SaveGamemode: на карте {mapId} нет ни одной сетки с BecomesStation — станцию создать нечем");
            return false;
        }

        var proto = FindGameMapPrototype(stationIds, primaryId);
        if (proto == null)
        {
            Log.Error($"SaveGamemode: не найден прототип игровой карты со станциями " +
                      $"{string.Join(", ", stationIds)} — конфигурацию станции взять неоткуда");
            return false;
        }

        // Сетки, для которых в выбранной конфигурации станции нет: движок на такую сетку пишет
        // "does not have an associated station config" и оставляет её без станции вовсе — у спасательных
        // капсул и древних кораблей свой BecomesStation. Такие сетки из события забираем и приписываем
        // к станции сами: на обычной карте раунда их приписывает тикер вместе с заселением.
        var unconfigured = new List<EntityUid>();
        for (var i = grids.Count - 1; i >= 0; i--)
        {
            var grid = grids[i];

            if (!TryComp<BecomesStationComponent>(grid, out var becomes) ||
                string.IsNullOrEmpty(becomes.Id) || proto.Stations.ContainsKey(becomes.Id))
            {
                continue;
            }

            unconfigured.Add(grid);
            grids.RemoveAt(i);
        }

        // Состояние читается ДО регистрации: имя станции нужно уже в PostGameMapLoad, чтобы станция
        // родилась с сохранённым именем, а не с выдуманным. Переименование после события слишком позднее:
        // к этому моменту имя уже стоит на сетках, в PDA и в шапках документов, которые печатаются
        // подставкой текста, а не ссылкой на станцию.
        _save.TryReadStationStates(slot, out var states);
        var byId = StatesById(states);

        var primaryName = (string?)null;
        if (primaryId != null && byId.TryGetValue(primaryId, out var primaryState) && IsStationName(primaryState.Name))
            primaryName = primaryState.Name;

        _gridsSuppressed.Clear();
        _registeringSavedMap = suppressRoundstartGrids;
        try
        {
            RaiseLocalEvent(new PostGameMapLoad(proto, mapId, grids, primaryName));
        }
        finally
        {
            _registeringSavedMap = false;
        }

        _savedMap = mapId;
        _savedStates = states;

        if (unconfigured.Count > 0)
        {
            TryStep("сетки без своей конфигурации станции", () => AttachUnconfiguredGrids(unconfigured, primaryGrid));
        }

        // Состояние из station.yml точнее, чем имя с сетки: там и балансы, и имя именно станции.
        // Кому состояния не досталось — тем возвращается имя с сетки.
        var fromState = new HashSet<EntityUid>();
        TryStep("состояния станций", () => ApplyStationStates(mapId, states, fromState));
        TryStep("имён станций с сеток", () => RestoreStationNames(savedNames, fromState));
        TryStep("имени станции в документах", FixStationNamesInDocuments);

        Log.Info($"SaveGamemode: станции {string.Join(", ", stationIds)} по конфигу карты {proto.ID} " +
                 $"зарегистрированы на карте {mapId} (сеток: {grids.Count})");
        return true;
    }

    /// <summary>
    ///     Приписывает сетки, для которых в выбранной конфигурации карты станции нет.
    /// </summary>
    private void AttachUnconfiguredGrids(List<EntityUid> unconfigured, EntityUid? primaryGrid)
    {
        if (primaryGrid != null &&
            TryComp<StationMemberComponent>(primaryGrid.Value, out var primaryMember))
        {
            var attached = 0;
            foreach (var grid in unconfigured)
            {
                if (HasComp<StationMemberComponent>(grid))
                    continue;

                // Имя передаётся своё: без него AddGridToStation оставил бы сетку безымянной,
                // а с именем станции переписал бы собственное имя точки интереса.
                _station.AddGridToStation(primaryMember.Station, grid, null, null, MetaData(grid).EntityName);
                attached++;
            }

            Log.Info($"SaveGamemode: {attached} сеток без своей конфигурации станции приписаны к " +
                     $"{ToPrettyString(primaryMember.Station)}");
        }
        else
        {
            Log.Error($"SaveGamemode: {unconfigured.Count} сеток остались без станции — главная сетка карты " +
                      "не стала станцией, конфигурация карты не подошла");
        }
    }

    /// <summary>
    ///     Возвращает станциям имена, которые были у них в сохранении.
    /// </summary>
    /// <remarks>
    ///     Станция — отдельная сущность вне карты, и к моменту сохранения она уже удалена: в файле её нет.
    ///     Но <c>InitializeNewStation</c> при регистрации раунда подписывает имя станции каждой её сетке
    ///     (<c>AddGridToStation</c> -> <c>MetaData</c>), поэтому в файле имя остаётся на сетках,
    ///     а <c>BecomesStationComponent.Id</c> говорит, какой станции какая сетка принадлежит.
    ///     Имя ставится после <c>PostGameMapLoad</c>, а не через него: событие передаёт одно имя
    ///     на всю карту, а станций на карте несколько — у спасательных капсул и древних кораблей
    ///     свои имена, и всем им одно не подходит.
    ///     Имя берётся самое частое среди сеток станции: у станции сеток несколько, и все они
    ///     подписаны одним именем, а сетка чужой станции сюда попасть не может — словарь собран
    ///     только по сеткам этой карты.
    ///     Станции, получившие состояние из <c>station.yml</c>, пропускаются: там имя точнее, чем то,
    ///     что осталось на сетке.
    /// </remarks>
    /// <param name="savedNames">Имена с сеток карты, собранные при загрузке.</param>
    /// <param name="skip">Станции, которым уже вернули имя из состояния.</param>
    private void RestoreStationNames(Dictionary<EntityUid, string> savedNames, HashSet<EntityUid>? skip = null)
    {
        if (savedNames.Count == 0)
        {
            Log.Info("SaveGamemode: имён станций в сохранении нет — имена будут сгенерированы заново");
            return;
        }

        var restored = 0;
        var query = AllEntityQuery<StationDataComponent>();
        while (query.MoveNext(out var station, out var data))
        {
            if (skip != null && skip.Contains(station))
                continue;

            string? name = null;
            var best = 0;
            Dictionary<string, int>? counts = null;

            foreach (var grid in data.Grids)
            {
                if (!savedNames.TryGetValue(grid, out var gridName))
                    continue;

                counts ??= new Dictionary<string, int>();
                var count = counts.TryAdd(gridName, 1) ? 1 : ++counts[gridName];
                if (count <= best)
                    continue;

                best = count;
                name = gridName;
            }

            if (name == null || Name(station) == name)
                continue;

            _documentNames[Name(station)] = name;
            _station.RenameStation(station, name, false, data);

            // Сетки при регистрации получили сгенерированное имя станции — приводим и их обратно,
            // как это делает AddGridToStation.
            foreach (var grid in data.Grids)
            {
                if (savedNames.ContainsKey(grid))
                    _metaData.SetEntityName(grid, name);
            }

            restored++;
            Log.Info($"SaveGamemode: станции {ToPrettyString(station)} возвращено имя из сохранения: {name}");
        }

        if (restored == 0)
            Log.Info("SaveGamemode: имена станций из сохранения не применены — уже совпадают или их не нашлось");
    }

    /// <summary>
    ///     Собирает состояние станций сохранённой карты: имя и балансы счетов.
    /// </summary>
    /// <remarks>
    ///     Вызывается на <c>RoundEndedEvent</c>, когда станции ещё живы: тикер удаляет их позже,
    ///     при переходе уровня игры в лобби. Берутся только станции с сетками на сохраняемой карте —
    ///     станции шаттлов и карт-хабов в слот сохранять нечего.
    ///     Станция привязана к карте через <c>BecomesStationComponent.Id</c> её сеток: это единственное,
    ///     что свяжет состояние с новой станцией при загрузке.
    /// </remarks>
    private List<SavedStationState> CollectStationStates(MapId mapId)
    {
        var states = new List<SavedStationState>();

        var query = AllEntityQuery<StationDataComponent>();
        while (query.MoveNext(out var station, out var data))
        {
            var ids = new List<string>();
            var gridNames = new List<string>();
            var gridsOnMap = 0;

            foreach (var grid in data.Grids)
            {
                if (!TryComp(grid, out TransformComponent? xform) || xform.MapID != mapId)
                    continue;

                gridsOnMap++;

                // Имя сетки — то, чем её можно будет найти в загруженной карте:
                // привязка сетки к станции файлом не сохраняется.
                var gridName = MetaData(grid).EntityName;
                if (!string.IsNullOrWhiteSpace(gridName))
                    gridNames.Add(gridName);

                if (TryComp<BecomesStationComponent>(grid, out var becomes) &&
                    !string.IsNullOrEmpty(becomes.Id) && !ids.Contains(becomes.Id))
                {
                    ids.Add(becomes.Id);
                }
            }

            if (gridsOnMap == 0)
                continue;

            var state = new SavedStationState { Name = Name(station) };
            state.Ids.AddRange(ids);
            state.GridNames.AddRange(gridNames);

            if (TryComp<StationBankAccountComponent>(station, out var bank))
            {
                foreach (var (account, money) in bank.Accounts)
                {
                    state.Accounts[account.Id] = money;
                }
            }

            states.Add(state);
            Log.Info($"SaveGamemode: станция {ToPrettyString(station)} — имя '{state.Name}', " +
                     $"сеток {state.GridNames.Count}, id [{string.Join(", ", ids)}], счетов {state.Accounts.Count}");
        }

        return states;
    }

    /// <summary>
    ///     Возвращает станциям имя и балансы счетов из <c>station.yml</c>.
    /// </summary>
    /// <remarks>
    ///     Станция при загрузке создаётся заново (<c>InitializeNewStation</c>) с новым выдуманным именем
    ///     и балансами из прототипа, поэтому состояние пишется рядом со слотом и применяется здесь —
    ///     сразу после регистрации станции, пока её никто не успел прочитать.
    ///     Искать станцию приходится по <c>BecomesStationComponent.Id</c> её сеток: сама станция в файле
    ///     не сериализуется, id — единственная метка, которая переживает сохранение.
    ///     Имя ставится и на станции, и на её сетках — <c>AddGridToStation</c> подписывает имя станции
    ///     сеткам, и печатные формы берут его именно со станции (<c>GetOwningStation</c> -> <c>Name</c>).
    /// </remarks>
    /// <param name="mapId">Карта, поднятая из сохранения.</param>
    /// <param name="states">Состояния станций, прочитанные из <c>station.yml</c>.</param>
    /// <param name="handledStations">Сюда записываются станции, получившие имя из состояния.</param>
    private void ApplyStationStates(MapId mapId, IReadOnlyList<SavedStationState> states, HashSet<EntityUid> handledStations)
    {
        if (states.Count == 0)
        {
            Log.Info($"SaveGamemode: состояния станций в слоте нет — имена возьму с сеток, " +
                     "балансы останутся заводскими");
            return;
        }

        var byId = StatesById(states);

        var applied = 0;
        var accounts = 0;
        var attached = 0;
        var used = new HashSet<SavedStationState>();

        // Сетки карты, которые ни к какой станции не привязаны: именно их ищем по имени из сохранения.
        // Основные сетки станций привязывает StationSystem при загрузке карты, они уже заняты.
        var free = new Dictionary<string, List<EntityUid>>();
        var gridEnum = AllEntityQuery<MapGridComponent, MetaDataComponent, TransformComponent>();
        while (gridEnum.MoveNext(out var uid, out _, out var meta, out var xform))
        {
            if (xform.MapID != mapId || HasComp<StationMemberComponent>(uid))
                continue;

            if (!free.TryAdd(meta.EntityName, new List<EntityUid> { uid }))
                free[meta.EntityName].Add(uid);
        }

        var query = AllEntityQuery<StationDataComponent>();
        while (query.MoveNext(out var station, out var data))
        {
            SavedStationState? state = null;
            var gridIds = new List<string>();
            var grids = new List<EntityUid>();

            foreach (var grid in data.Grids)
            {
                if (!TryComp(grid, out TransformComponent? xform) || xform.MapID != mapId)
                    continue;

                grids.Add(grid);

                if (!TryComp<BecomesStationComponent>(grid, out var becomes) || string.IsNullOrEmpty(becomes.Id))
                    continue;

                gridIds.Add(becomes.Id);
                if (state == null && byId.TryGetValue(becomes.Id, out var found))
                    state = found;
            }

            if (grids.Count == 0)
                continue;

            if (state == null)
            {
                Log.Warning($"SaveGamemode: для станции {ToPrettyString(station)} с сетками id " +
                            $"[{string.Join(", ", gridIds)}] состояния в '{SaveSystem.StationFileName}' нет — " +
                            $"имя '{Name(station)}' осталось сгенерированным");
                continue;
            }

            used.Add(state);
            applied++;
            handledStations.Add(station);

            var oldName = Name(station);
            if (IsStationName(state.Name) && oldName != state.Name)
            {
                _station.RenameStation(station, state.Name, false, data);
                _documentNames[oldName] = state.Name;

                foreach (var grid in grids)
                {
                    if (MetaData(grid).EntityName == oldName)
                        _metaData.SetEntityName(grid, state.Name);
                }

                Log.Info($"SaveGamemode: станции {ToPrettyString(station)} (id [{string.Join(", ", gridIds)}]) " +
                         $"возвращено имя из сохранения: '{oldName}' -> '{state.Name}'");
            }
            else if (!IsStationName(state.Name))
            {
                Log.Warning($"SaveGamemode: в '{SaveSystem.StationFileName}' у станции нет имени — " +
                            $"остаётся '{oldName}'");
            }

            attached += AttachStationGrids(station, data, state, free);
            accounts += ApplyStationAccounts(station, state);
        }

        foreach (var state in states)
        {
            if (used.Contains(state))
                continue;

            Log.Warning($"SaveGamemode: состояние станции '{state.Name}' (id [{string.Join(", ", state.Ids)}]) " +
                        $"не применено — станции с такими сетками на карте {mapId} нет");
        }

        var left = 0;
        foreach (var candidates in free.Values)
        {
            left += candidates.Count;
        }

        if (left > 0)
        {
            Log.Info($"SaveGamemode: сеток без станции на карте {mapId} осталось: {left} — " +
                     $"в '{SaveSystem.StationFileName}' для них станции нет");
        }

        Log.Info($"SaveGamemode: из состояния применено станций: {applied}, сеток возвращено: {attached}, " +
                 $"счетов восстановлено: {accounts}");
    }

    /// <summary>
    ///     Возвращает станции её шаттлы, спасательные капсулы и точки интереса.
    /// </summary>
    /// <remarks>
    ///     Привязка сетки к станции (<c>StationMemberComponent</c>) — живая ссылка на станцию, которой в файле
    ///     нет, поэтому после загрузки шаттл карго или капсула висят «ничейными»: всё, что ищет станцию
    ///     по сетке (банк карго, терминал заказов, доступы дверей), на них не работает.
    ///     Сетка берётся из пула по имени один раз: одинаковых капсул у станции несколько, и каждая запись
    ///     в сохранении — отдельная сетка. Имя передаётся своё, иначе <c>AddGridToStation</c> перепишет его
    ///     именем станции, а у точек интереса оно собственное.
    /// </remarks>
    private int AttachStationGrids(EntityUid station, StationDataComponent data, SavedStationState state,
        Dictionary<string, List<EntityUid>> free)
    {
        var attached = 0;

        foreach (var gridName in state.GridNames)
        {
            if (!free.TryGetValue(gridName, out var candidates) || candidates.Count == 0)
                continue;

            var grid = candidates[^1];
            candidates.RemoveAt(candidates.Count - 1);

            _station.AddGridToStation(station, grid, null, data, gridName);
            attached++;
        }

        return attached;
    }

    /// <summary>
    ///     Ставит балансы счетов станции из сохранения.
    /// </summary>
    /// <returns>Сколько счетов удалось восстановить.</returns>
    /// <remarks>
    ///     Счёт берётся по своему id: прототипы счетов могли измениться между раундами, и незнакомый id
    ///     движок не примет. Счета создаются вместе со станцией из её прототипа, но если компонента нет —
    ///     он заводится, иначе сохранённые деньги девать некуда.
    /// </remarks>
    private int ApplyStationAccounts(EntityUid station, SavedStationState state)
    {
        if (state.Accounts.Count == 0)
            return 0;

        var bank = EnsureComp<StationBankAccountComponent>(station);
        var restored = 0;

        foreach (var (account, money) in state.Accounts)
        {
            if (!_proto.HasIndex<CargoAccountPrototype>(account))
            {
                Log.Warning($"SaveGamemode: счёт '{account}' из сохранения не найден среди прототипов — пропущен");
                continue;
            }

            if (_cargo.TrySetBankAccount((station, bank), account, money, createAccount: true))
                restored++;
        }

        if (restored > 0)
        {
            Log.Info($"SaveGamemode: станции {ToPrettyString(station)} восстановлено счетов: {restored} " +
                     $"из {state.Accounts.Count}");
        }

        return restored;
    }

    /// <summary>
    ///     Похоже ли имя сетки на имя станции, а не на то, с которым сетку создал движок.
    /// </summary>
    /// <remarks>
    ///     <c>MapManager</c> создаёт сущность сетки с именем <c>grid</c>. Если в файле стоит оно,
    ///     сетку при раунде не переименовывали: станция была безымянной, и выдумывать из этого имя не стоит.
    /// </remarks>
    private static bool IsStationName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && !name.Equals(DefaultGridName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Правит сохранённое имя станции в документах, которые уже напечатаны.
    /// </summary>
    /// <remarks>
    ///     Шапка печатной формы — не ссылка на станцию, а подставленный в текст шаблон:
    ///     <c>STATION XX-00</c> в формах <c>Resources/Paperwork</c> заменяется именем станции в момент
    ///     печати (<c>PaperworkTextSubstitutions.ApplyBase</c>). Документ, напечатанный, пока станция
    ///     носила выдуманное имя, остаётся с ним навсегда — переименование станции его не касается,
    ///     и в следующее сохранение уходит уже испорченный текст. Поэтому бумаги проходятся один раз
    ///     за раунд, и выдуманное имя в них меняется на сохранённое. Подписи документов правятся там же:
    ///     имя станции встречается и в строке подписи.
    /// </remarks>
    private void FixStationNamesInDocuments()
    {
        if (_documentNames.Count == 0)
            return;

        var papers = 0;
        var signatures = 0;

        var query = AllEntityQuery<PaperComponent>();
        while (query.MoveNext(out var uid, out var paper))
        {
            var changed = false;

            var content = paper.Content;
            if (!string.IsNullOrEmpty(content))
            {
                foreach (var (wrong, right) in _documentNames)
                {
                    if (!content.Contains(wrong, StringComparison.Ordinal))
                        continue;

                    content = content.Replace(wrong, right);
                    changed = true;
                }

                if (changed)
                {
                    paper.Content = content;
                    papers++;
                }
            }

            for (var i = 0; i < paper.Signatures.Count; i++)
            {
                var line = paper.Signatures[i];

                foreach (var (wrong, right) in _documentNames)
                {
                    if (!line.Contains(wrong, StringComparison.Ordinal))
                        continue;

                    line = line.Replace(wrong, right);
                }

                if (line != paper.Signatures[i])
                {
                    paper.Signatures[i] = line;
                    signatures++;
                    changed = true;
                }
            }

            if (changed)
                Dirty(uid, paper);
        }

        if (papers == 0 && signatures == 0)
            return;

        var pairs = new List<string>();
        foreach (var (wrong, right) in _documentNames)
        {
            pairs.Add($"'{wrong}' -> '{right}'");
        }

        Log.Info($"SaveGamemode: имя станции поправлено в уже напечатанных документах: бумаг {papers}, " +
                 $"подписей {signatures} ({string.Join(", ", pairs)})");
    }

    /// <summary>
    ///     Ищет прототип игровой карты, в конфигурации которого есть станции с такими именами.
    /// </summary>
    /// <remarks>
    ///     Имя сохранённой карты для этого не подходит: станции ищутся по <c>BecomesStationComponent.Id</c>
    ///     в словаре <c>stations</c> прототипа, и у одной карты их несколько. Карта, которую режим отменил
    ///     ради сохранения, — эталон, проверяем её первой, чтобы не начать грузить конфигурацию чужой станции.
    ///     Подходящих прототипов бывает несколько, и брать первый нельзя: на карте Box станции
    ///     <c>Boxstation</c> и <c>Empty</c> (капсулы), конфигурация <c>Box</c> знает только первую, а
    ///     отладочная <c>Empty</c> — только капсулы. Взять её значит собрать станцию из капсул, а главную
    ///     сетку оставить вовсе без станции. Поэтому считается, сколько станций прототип накрывает, и
    ///     главное — есть ли у него конфигурация для id главной (самой большой) сетки карты.
    /// </remarks>
    /// <param name="stationIds">Все <c>BecomesStationComponent.Id</c> сеток карты.</param>
    /// <param name="primaryId">Id главной сетки карты, если она есть.</param>
    private GameMapPrototype? FindGameMapPrototype(HashSet<string> stationIds, string? primaryId = null)
    {
        GameMapPrototype? best = null;
        var bestScore = 0;

        foreach (var proto in Candidates())
        {
            var covered = 0;
            foreach (var stationId in stationIds)
            {
                if (proto.Stations.ContainsKey(stationId))
                    covered++;
            }

            if (covered == 0)
                continue;

            // Полное покрытие и конфигурация главной сетки важнее, чем число накрытых станций:
            // станции на карте заведомо чужие (капсулы), а станции без работы и шаттла быть не может.
            var score = covered + (primaryId != null && proto.Stations.ContainsKey(primaryId) ? stationIds.Count : 0);
            if (score <= bestScore)
                continue;

            bestScore = score;
            best = proto;
        }

        return best;
    }

    private IEnumerable<GameMapPrototype> Candidates()
    {
        foreach (var proto in _replacedMaps)
        {
            yield return proto;
        }

        foreach (var proto in _gameMap.AllMaps())
        {
            yield return proto;
        }
    }

    /// <summary>
    ///     Будит NPC на загруженной карте.
    /// </summary>
    /// <remarks>
    ///     NPC будит только <c>OnNPCMapInit</c>, а <c>WakeNPC</c> — это <c>EnsureComp&lt;ActiveNPCComponent&gt;</c>,
    ///     без которого <c>HTNSystem.UpdateNPC</c> сущность не обрабатывает вовсе: существо стоит и ни на что
    ///     не реагирует. Из сохранения MapInit не приходит — флаг <c>mapInit</c> в файле стоит именно затем.
    ///     Спальным режимом по расстоянию это не лечится: <c>NPCSystem</c> будит лишь тех, кого усыпил сам.
    ///     Дальние существа он усыпит обратно за пятнадцать секунд, цена — одна волна планирования на старте.
    /// </remarks>
    private void WakeLoadedNpcs(MapId mapId)
    {
        var woken = 0;
        var query = AllEntityQuery<HTNComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapID != mapId || TerminatingOrDeleted(uid))
                continue;

            _npc.WakeNPC(uid);
            woken++;
        }

        Log.Info($"SaveGamemode: разбужено NPC на загруженной карте: {woken}");
    }

    /// <summary>
    ///     Возвращает в сети устройств приборы, поднятые из сохранения.
    /// </summary>
    /// <remarks>
    ///     Прибор вступает в сеть устройств только в <c>MapInit</c>: <c>DeviceNetworkSystem.OnMapInit</c>
    ///     подставляет частоты из прототипа и зовёт <c>ConnectDevice</c>. Сохранённые сущности приходят
    ///     с флагом <c>mapInit</c> и повторного MapInit не видят, поэтому остаются вне сети: адрес и частоты
    ///     в файле есть, списки связей мультитул показывает, но получателя в сети нет и пакет никуда не идёт.
    ///     Молчат и сохранённые кнопки, и сохранённые двери, и свежие связки «новая кнопка — старая дверь».
    ///     Отключённые вручную приборы не трогаем: <c>DisconnectDevice</c> выставляет <c>AutoConnect = false</c>.
    /// </remarks>
    private void ConnectLoadedDevices(MapId mapId)
    {
        var connected = 0;
        var renamed = 0;
        var refused = 0;

        var query = AllEntityQuery<DeviceNetworkComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var device, out var xform))
        {
            if (xform.MapID != mapId || TerminatingOrDeleted(uid))
                continue;

            if (!device.AutoConnect || _deviceNet.IsDeviceConnected(uid, device))
                continue;

            var address = device.Address;

            if (!_deviceNet.ConnectDevice(uid, device))
            {
                // Адрес занят другим прибором: в сеть устройство не попало, связи по нему работать не будут.
                refused++;
                continue;
            }

            // Частоты из прототипа подставляет тот же MapInit. В файле они обычно уже посчитаны,
            // но если достался файл до их записи — восстанавливаем через сеть, чтобы она не разошлась.
            if (device.ReceiveFrequency == null && device.ReceiveFrequencyId != null &&
                _proto.TryIndex<DeviceFrequencyPrototype>(device.ReceiveFrequencyId, out var receive))
            {
                _deviceNet.SetReceiveFrequency(uid, receive.Frequency, device);
            }

            if (device.TransmitFrequency == null && device.TransmitFrequencyId != null &&
                _proto.TryIndex<DeviceFrequencyPrototype>(device.TransmitFrequencyId, out var transmit))
            {
                _deviceNet.SetTransmitFrequency(uid, transmit.Frequency, device);
            }

            connected++;

            if (address != device.Address)
                renamed++;
        }

        Log.Info($"SaveGamemode: в сети устройств на загруженной карте: {connected} приборов" +
                 (renamed > 0 ? $", из них {renamed} получили новый адрес (связи по адресу их не найдут)" : string.Empty) +
                 (refused > 0 ? $", отказано {refused}" : string.Empty));
    }

    /// <summary>
    ///     Одна попытка загрузить сохранение на <paramref name="defaultMap"/> вместо карты, созданной движком.
    /// </summary>
    /// <remarks>
    ///     Загрузка битого сохранения бросает исключение из читателя компонентов
    ///     (<c>RequiredFieldNotMappedException</c>, <c>KeyNotFoundException</c> в сериализаторе DecalGrid),
    ///     и без обёртки оно прилетает в <c>GameTicker.StartRound</c>, роняя сервер.
    ///     Инициализацию карты оставляем тикеру, иначе он получит «Map is already initialized».
    /// </remarks>
    private bool TryLoadOnce(MapId defaultMap, ResPath path, out Entity<MapComponent>? map)
    {
        map = null;

        // Удаляем карту, созданную движком (или оставшуюся от оборвавшейся загрузки),
        // и загружаем сохранение с тем же MapId.
        if (!TryClearMapSlot(defaultMap))
            return false;

        try
        {
            if (!_mapLoader.TryLoadMapWithId(defaultMap, path, out map, out _, DeserializationOptions.Default))
            {
                map = null;
                return false;
            }

            return map != null;
        }
        catch (Exception e)
        {
            Log.Error($"SaveGamemode: загрузка сохранения из {path} бросила исключение");
            Log.Error(e.ToString());
            map = null;
            return false;
        }
    }

    /// <summary>
    ///     Можно ли снова пользоваться <paramref name="mapId"/>: карты нет, либо она обычная.
    /// </summary>
    /// <remarks>
    ///     Оборвавшаяся загрузка оставляет карту в состоянии удаления: движок сам начинает вычищать
    ///     недосозданные сущности (<c>MapLoaderSystem.Delete</c>) и падает на середине. Такую карту нельзя
    ///     ни удалить, ни освободить, а попытки это сделать бросают исключение в <c>StartRound</c>.
    /// </remarks>
    private bool CanReuseMapSlot(MapId mapId)
    {
        if (!_mapSystem.TryGetMap(mapId, out var mapUid) || !Exists(mapUid))
            return true;

        return MetaData(mapUid!.Value).EntityLifeStage < EntityLifeStage.Terminating;
    }

    /// <summary>
    ///     Освободить <paramref name="mapId"/> под новую загрузку.
    /// </summary>
    /// <returns>
    ///     False, если карту нельзя ни удалить, ни переиспользовать — вызывающий обязан уйти
    ///     на запасную карту, а не пытаться грузиться на занятый MapId.
    /// </returns>
    private bool TryClearMapSlot(MapId mapId)
    {
        if (!_mapSystem.TryGetMap(mapId, out var mapUid) || !Exists(mapUid))
            return true;

        if (!CanReuseMapSlot(mapId))
        {
            Log.Error($"SaveGamemode: карта {mapId} зависла в состоянии удаления после оборвавшейся загрузки");
            return false;
        }

        try
        {
            _mapSystem.DeleteMap(mapId);
            return true;
        }
        catch (Exception e)
        {
            Log.Error($"SaveGamemode: не удалось удалить карту {mapId} перед загрузкой: {e}");
            return false;
        }
    }

    /// <summary>
    ///     Загружает вместо сохранения карты, которые режим отменил при старте раунда.
    /// </summary>
    /// <remarks>
    ///     Сохранение может быть синтаксически целым, но нечитаемым по составу, и тогда загрузка падает
    ///     уже после проверки файла. Раунд обязан стартовать на реальной станции: пустая карта грузится,
    ///     но распределение работ падает на отсутствии станций.
    /// </remarks>
    private bool TryRestoreReplacedMaps(MapId defaultMap)
    {
        // Fallback-карты могли не запомниться (правило запустили вручную) — берём текущую выбранную карту.
        var maps = _replacedMaps.Count > 0
            ? _replacedMaps
            : _gameMap.GetSelectedMap() is { } selected
                ? new List<GameMapPrototype> { selected }
                : new List<GameMapPrototype>();

        if (maps.Count == 0)
        {
            Log.Error("SaveGamemode: нечего возвращать вместо сохранения — выбранная карта не определена");
            return false;
        }

        for (var i = 0; i < maps.Count; i++)
        {
            var proto = maps[i];
            try
            {
                // Тот же путь, что у обычного старта раунда: LoadGameMapWithId поднимает
                // Pre/PostGameMapLoad, чтобы станция и гриды зарегистрировались как обычно.
                // Карту не инициализируем — это делает GameTicker.StartRound.
                if (i == 0)
                {
                    if (_mapSystem.MapExists(defaultMap))
                        _mapSystem.DeleteMap(defaultMap);

                    GameTicker.LoadGameMapWithId(proto, defaultMap, DeserializationOptions.Default);

                    Log.Warning($"SaveGamemode: вместо сохранения загружена карта '{proto.ID}' на {defaultMap}");
                }
                else
                {
                    GameTicker.LoadGameMap(proto, out var extraMap, DeserializationOptions.Default);
                    Log.Warning($"SaveGamemode: вместо сохранения загружена карта '{proto.ID}' на {extraMap}");
                }
            }
            catch (Exception e)
            {
                Log.Error($"SaveGamemode: не удалось загрузить карту '{proto.ID}' вместо сохранения: {e}");
                // Недосозданная карта на нужном MapId помешает и спавну, и следующей попытке.
                TryDeleteMap(defaultMap);
                return false;
            }
        }

        return _mapSystem.MapExists(defaultMap);
    }

    /// <summary>
    ///     Крайний случай: ни сохранения, ни обычной карты. Пустая карта лучше падения старта раунда
    ///     на отсутствующей карте, но станции на ней нет — раунд без игроков и работ.
    /// </summary>
    private void TryCreateEmptyMap(MapId defaultMap)
    {
        try
        {
            if (_mapSystem.MapExists(defaultMap))
                _mapSystem.DeleteMap(defaultMap);

            _mapSystem.CreateMap(defaultMap, runMapInit: false);
            Log.Error($"SaveGamemode: вместо сохранения создана пустая карта {defaultMap} — " +
                      "на ней нет станции, раунд на ней играть нельзя");
        }
        catch (Exception e)
        {
            Log.Error($"SaveGamemode: не удалось восстановить пустую карту {defaultMap}: {e}");
        }
    }

    private void TryDeleteMap(MapId mapId)
    {
        try
        {
            if (_mapSystem.MapExists(mapId))
                _mapSystem.DeleteMap(mapId);
        }
        catch (Exception e)
        {
            Log.Error($"SaveGamemode: не удалось удалить карту {mapId}: {e}");
        }
    }

    private void TrySaveCurrentMap(SaveGamemodeRuleComponent component)
    {
        var mapId = GameTicker.DefaultMap;
        if (mapId == MapId.Nullspace)
        {
            Log.Warning("SaveGamemode: DefaultMap не установлен, сохранение пропущено");
            return;
        }

        Log.Info($"SaveGamemode: попытка сохранить карту {mapId} в слот '{component.SaveSlot}'");

        // Состояние станций снимается ДО записи карты: запись идёт в память минуту, и за это время
        // станции никто не трогает, но и тянуть её после долгого сохранения не за чем.
        var states = CollectStationStates(mapId);

        // Сохранение через пост-процессор: он вычищает runtime-состояние и снимает mapInit,
        // чтобы карта загрузилась как неинициализированная.
        if (!_save.TrySaveFilteredSlot(mapId, component.SaveSlot, out var report))
        {
            Log.Error($"SaveGamemode: не удалось сохранить карту {mapId} в слот '{component.SaveSlot}' " +
                      $"(старое сохранение слота не тронуто)");
            return;
        }

        Log.Info($"SaveGamemode: карта {mapId} сохранена в слот '{component.SaveSlot}'. {report}");

        // Станция — сущность вне карты, в файл карты она не попадает: её имя и балансы пишутся
        // рядом в station.yml. Без них станция при загрузке рождается заново — с новым именем
        // и деньгами из прототипа.
        if (_save.TryWriteStationStates(component.SaveSlot, states))
            Log.Info($"SaveGamemode: состояние станций сохранено в слот '{component.SaveSlot}' " +
                     $"(станций: {states.Count})");
        else
            Log.Error($"SaveGamemode: не удалось записать состояние станций в слот '{component.SaveSlot}' — " +
                      "при загрузке имена будут взяты с сеток, балансы — из прототипа");
    }
}
