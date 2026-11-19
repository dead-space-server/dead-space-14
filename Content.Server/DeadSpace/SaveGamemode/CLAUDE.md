# SaveGamemode — подсказка для работы с кодом

Режим: в конце раунда карта сохраняется в файл, в начале следующего раунда поднимается из файла
и регистрируется как станция. Всё чинится в основном на ЗАГРУЗКЕ — старые сохранения должны
работать без пересохранения.

## Файлы

- `SaveGamemodeRuleSystem.cs` — правило раунда: загрузка на `Started`, сохранение на `RoundEndedEvent`,
  регистрация станции, применение состояния. Центр всего.
- `SaveSystem.cs`, `SaveSystem.Stations.cs` — ввод-вывод: `map.yml` — карта через
  `_mapLoader.SerializeEntitiesRecursive` с фильтрами; `station.yml` — состояние станций
  (имя, `Ids` сеток по `BecomesStation`, счета, имена сеток).
  Путь: `/Saves/GameMode/<slot>/` (на сервере, НЕ в этом репозитории — здесь сейвов нет).
- `SaveGamemodeRuleComponent.cs` — настройка правила: `saveSlot` и `perMapSlot`.
  `perMapSlot: true` — слот свой для каждой карты, имя = id прототипа выбранной карты
  (`ResolveSlot`, санитайз `SanitizeSlot`): папки нет — раунд на обычной карте и в конце
  сохраняется в новую папку; папка есть — поднимается из неё. `perMapSlot: false` — общий слот
  `saveSlot`, как у пары `SaveGamemodeDeathmatch`/`SaveGamemodeExtended`.
  Конец раунда обязан брать то же имя, что и старт: `GameMapManager` после раунда уже выбирает
  СЛЕДУЮЩУЮ карту, поэтому id карты запоминается на `OnLoadingMaps` в `_roundMapId`
  (`GetSelectedMap()` — только фолбэк для запуска правил вне стандартного старта).
- `MapFilterConfig.cs` — списки фильтров для записи карты.
- `MapStateFilter.cs` — постобработка YAML: корневые списки `maps/grids/orphans/nullspace`,
  срезание компонентов с мёртвыми ссылками.

## Порядок загрузки (главный путь)

1. Карта поднимается из `map.yml` (MapInit suppressed — флаг `mapInit` в файле).
2. `TryRegisterStations`:
   - `PurgeSavedStations` — удаляет сущности станций, пришедшие в файле (см. ловушку 1);
   - сетки без `StationMember` собираются, главная = самая большая по тайлам → `primaryId`;
   - `FindGameMapPrototype(stationIds, primaryId)` — прототип карты берётся ПО `primaryId` (ловушка 4);
   - сетки без конфигурации станции в прототипе вынимаются из события → `AttachUnconfiguredGrids`;
   - имя из `station.yml` читается ДО `PostGameMapLoad` и передаётся в него;
   - `RaiseLocalEvent(new PostGameMapLoad(proto, mapId, grids, name))` → `StationSystem.InitializeNewStation`
     → `StationPostInitEvent` → на нём `EmergencyShuttleSystem` вешает эвакошаттл и ЦентКом;
   - `ApplyStationStates` (имена/счета по `Ids`) → `RestoreStationNames`/`AttachStationGrids` —
     фолбэки для старых сейвов без станций в файле.
3. `WakeLoadedNpcs` — MapInit не приходил, NPC спят. Следом `FixLoadedUseDelays`, `FixLoadedGuns`,
   `FixLoadedItemCabinets`, `FixLoadedItemSlotLocks` — то состояние, которое навёл бы MapInit, а в файле
   его нет (см. ловушку 7).
4. Окно `_registeringSavedMap` подавляет спавн обязательных раундстарт-сеток при регистрации.

## Ловушки (проверено на живой проблеме с стыковкой эвака)

1. **Станция сериализуется в `map.yml`**, хотя «не часть карты»: на `RoundEndedEvent` она ещё жива
   (`StationSystem` удалит её позже, на `PreRoundLobby`), сетки пишутся со ссылкой
   `StationMember.station`, а `EntitySerializer._autoInclude` затаскивает её в файл в `nullspace`.
   Без `PurgeSavedStations` получалась вторая станция из конфигурации: шаттл/ЦентКом — на новой,
   главная сетка с игроками — на старой без шаттла →
   `Attempted to call an emergency shuttle for an uninitialized station` (EmergencyShuttleSystem.cs:279)
   и отсутствие стыковки. `DockEmergencyShuttle()` обходит ВСЕ `StationEmergencyShuttleComponent`.
2. `StationNameSetupComponent.ComponentInit` при десериализации перегенерирует случайное имя —
   поэтому имя из сохранения надо передавать в `PostGameMapLoad`, переименование после события
   запаздывает (имя уже в PDA и документах).
3. `ComponentsNeedingLiveLink` срезает компонент только если ссылка мертва — на момент сохранения
   ссылки живы, так что «вырезать станцию на записи» этим не работает; вся корректная обработка —
   на загрузке.
4. Id `Empty`: у спаскапсул `BecomesStation.Id = "Empty"`, и в `_gameMap.AllMaps()` есть чужой
   прототип карты со станцией `Empty`. Без приоритета `primaryId` станция собиралась из капсул.
5. У Box-карты (`Resources/Prototypes/Maps/box.yml`) в `stations:` только `Boxstation` — остальные
   id (капсулы и пр.) движок игнорирует, их дописываем сами.
6. Один источник правды: `map.yml` — геометрия/сущности, `station.yml` — имена и счета. Станция
   из файла для нас мусорная, её имя всё равно regenerates (см. 2).
7. **Runtime-словари, которые заполняет только MapInit, в файле пусты — и предмет молча ломается.**
   Разбор на живом примере «PhotonShotgun не берётся в две руки и не стреляет»:
   - `UseDelayComponent.Delays` срезается при записи (`MapFilterConfig`: `StripAllComponents` +
     `AddDropped("UseDelay", "delays")`), а запись «default» создаёт только `UseDelaySystem.OnMapInit`
     (`SetLength(comp.Delay, DefaultId)`). MapInit сохранённой сущности не приходит → словарь пуст →
     `TryResetDelay` отказывает не «потому что ещё идёт», а «потому что нет записи».
   - `SharedWieldableSystem.TryWield` зовёт `TryResetDelay(used, checkDelayed: true)` и на отказе молча
     возвращает false (к тому же `OnUseInHand` всегда ставит `args.Handled = true`) → ни взятия, ни попапа.
     Отсюда «сломан только один прототип»: у оружия без `UseDelay` взятие работает, а `PhotonShotgun` —
     `UseDelay: 1.2` + `GunRequiresWield`, значит и не стреляет тоже. Тем же путём отказывает `SpinCylinder`.
   - Старые файлы писали `delays` как есть: `EndTime` — абсолютное время ТОГО сервера, `CurTime` нового
     начинается с нуля → `IsDelayed` истина почти до конца раунда.
   - Тот же ряд: `GunComponent.*Modified` (считает только `RefreshModifiers` из `OnMapInit`; без него
     `FireRateModified == 0`, и `AttemptShoot` падает на `TimeSpan.FromSeconds(1f / 0f)`), залипший
     `Wieldable.Wielded` (вторая рука — виртуальная, её в файле нет; с `unwieldOnUse: false` клик по
     «уже взятому» оружию только крутит режимы), замки `ItemSlots` и `ItemCabinet` (их наводит
     `MapInit`/`LockToggledEvent`).
   - `heldPrefix`: `ItemComponent.HeldPrefix` — поле прототипа, в сохранении его нет; берём из
     `MetaData(uid).EntityPrototype`.

## Диагностика по логам сервера

- `Set up station <имя> (<uid>)` раньше строки «сохранение ... загружено» = станция пришла из файла;
  позже = её создала регистрация. Станций должна быть ровно одна на карту.
- `SaveGamemode: с файлом карты пришли сущности станций (N) — убраны до регистрации...` — purge сработал.
- `SaveGamemode: подмена стандартной карты сохранением '<имя>'` — слот найден и файл годен; `<имя>` должно
  равняться id карты при `perMapSlot`. `сохранения '<имя>' нет ... раунд идёт на стандартной карте` —
  первый раунд на этой карте (папка появится в конце раунда), это не ошибка.
- `Discovered invalid centcomm component?` — десериализованный `StationCentcommComponent` с мёртвыми
  ссылками; вместе с purged-станциями исчезает.
- На конце раунда должно быть `Emergency shuttle ... docked with stations`, без ошибки про uninitialized.
- Структура `map.yml`: корни `maps/grids/orphans/nullspace`; станции — в `nullspace`.
- После «сохранение ... загружено» обязательны строки починки состояния:
  `задержки использования на загруженной карте: заведено N, погашено M`,
  `оружие на загруженной карте: обновлено N`, `снято залипших «взято с двух рук»: M`,
  `замков слотов предметов обновлено: N`, `шкафов предметов: закрыто/открыто ...`,
  `разбужено NPC на загруженной карте: N`. Нули или отсутствие строки = правки до сущностей не дошли:
  сначала проверь, что сервер запущен на свежей сборке (время `Content.Server.dll` новее исходника).

## Напутствия

- Чини на загрузке, а не на записи: исправление должно чинить уже существующие сохранения.
- Не рефактори `StationSystem` / `EmergencyShuttleSystem` / движок ради режима — правки только здесь,
  с маркерами `// DS14-start` / `// DS14-end` если трогаешь чужое.
- Прогоняй мысленно три сценария: свежий сейв / старый сейв совсем без станции в файле / сейв со
  станцией в файле (текущий случай). Фолбэки `RestoreStationNames`/`AttachStationGrids` обязаны
  продолжать работать.
- Комментарии в этой папке на русском, namespace file-scoped, стиль как в остальных файлах папки.
- Сборка: `dotnet build H:\dead-space-14\SpaceStation14.slnx --configuration DebugOpt`
  (в корне `.slnx`, не `.sln`; можно точечно `Content.Server\Content.Server.csproj`).
- Правка считается сделанной только после успешной сборки: анализатор прав доступа роняет её на ровном
  месте (RA0002 — вызов метода на поле чужого компонента, например `itemSlots.Slots.TryGetValue`; бери
  слот через `_itemSlots.TryGetSlot`). Сборка упала — `dotnet run` сервер не запустит, а запуск
  готового файла уйдёт на СТАРУЮ сборку, и будет казаться, что правка не помогла. Проверяй время
  `Content.Server.dll` и присутствие новой строки лога в `bin\Content.Server\Content.Server.dll`.
- Сейвы и логи живут на установке сервера, в этом репозитории их нет — не трать время на поиск.
