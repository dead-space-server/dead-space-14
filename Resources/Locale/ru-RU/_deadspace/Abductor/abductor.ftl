# ============================================================================
# Abductor (DeadSpace port) - ru-RU
# ============================================================================

# Roles / role types
abductor-role-name = Абдуктор
abductor-victim-role-name = Похищенный
abductor-victim-role-name-freeagent = Жертва похищения

# Ghost roles
abductor-lone-ghost-role-name = Абдуктор-одиночка
abductor-lone-ghost-role-desc = Ваш корабль сел на якорь рядом со станцией. Выберите цель, похитьте её и доставьте на корабль для экспериментов.
abductor-scientist-ghost-role-name = Учёный-абдуктор
abductor-scientist-ghost-role-desc = Вы учёный-абдуктор. Ваша задача — найти подходящую жертву, затянуть её на площадку и поставить эксперимент.
abductor-agent-ghost-role-name = Агент-абдуктор
abductor-agent-ghost-role-desc = Вы агент-абдуктор. Ваша задача — защищать учёного и помогать захватывать цели.
abductors-ghost-role-rules = Вы антагонист и призрачная роль. Не грифите и соблюдайте правила сервера.

# Briefings
abductor-role-greeting = Вы абдуктор! Работайте в команде: похищайте членов экипажа, ставьте на них эксперименты и возвращайте их обратно. Не забудьте вернуть подопытных целыми и невредимыми (почти).
abductor-victim-role-greeting = Вас похитили! Инопланетяне что-то с вами сделали и выбросили вас обратно на станцию. Ваша цель указана в анкете персонажа. Удачи.

# Antag objectives
roles-antag-abductor-objective = Похищайте членов экипажа и ставьте на них эксперименты.
roles-antag-abductor-victim = Выполните задание, данное абдукторами.

# Subtypes
role-subtype-abductor = Команда абдукторов
role-subtype-abductor-victim = Жертва похищения

# Objective condition
objective-condition-abduct-title = Похищение целей
objective-condition-abduct-description = Похитите {$count} членов экипажа. Они должны стоять на инопланетной площадке, когда вы нажмёте «Притянуть».

objective-issuer-abductors = Абдукторы
objective-issuer-voices = Голоса

# Console UI
abductor-ui-pad-found = Инопланетная площадка найдена.
abductor-ui-pad-not-found = Инопланетная площадка не найдена.
abductor-ui-target-none = Цели нет.
abductor-ui-target-found = Цель: {$target}
abductor-ui-experimentator-connected = Экспериментатор подключён.
abductor-ui-experimentator-not-found = Экспериментатор не найден. Не подпускайте подопытного к экспериментатору, чтобы не сбросить метку.
abductor-ui-victim-none = Экспериментатор пуст.
abductor-ui-victim-found = Подопытный в экспериментаторе: {$victim}
abductor-ui-armor-plug-in = Подключите жилет к консоли, чтобы управлять им.
abductors-ui-lock-armor = Заблокировать броню
abductors-ui-unlock-armor = Разблокировать броню
abductors-ui-teleport = Телепорт
abductors-ui-experiment = Эксперимент
abductors-ui-armor-control = Управление бронёй
abductors-ui-attract = Притянуть
abductors-ui-complete-experiment = Завершить эксперимент
abductors-ui-combat-mode = Боевой режим
abductors-ui-stealth-mode = Скрытный режим
abductors-ui-beacons = Маяки

# Window titles and extra UI
abductor-ui-console-title = Консоль абдуктора
abductor-camera-console-title = Перехваченные камеры
abductor-ui-stations-title = Станции
abductor-ui-station-title = Станция — {$station}
abductor-ui-back-to-stations = < Станции

# Gizmo / vest popups
abductors-ui-gizmo-transferred = Цель помечена. Координаты отправлены на консоль наблюдения.
abductors-ui-vest-linked = Жилет привязан к этой консоли.

# Gland implanting
gland-implanted-popup = Железа впивается под кожу!

# Flavor
flavor-base-alienblood = Скользкий и слегка сладковатый, с горьким металлическим привкусом.

# ItemSwitch verb category
verb-categories-switch = Переключить

# Species / reagent / tiles
species-name-abductor = Абдуктор
reagent-name-alien-blood = кровь инопланетянина
reagent-desc-alien-blood = Жидкость, текущая в жилах абдукторов.
tiles-abductor-floor = пол абдукторов

# ============================================================================
# Abductor entities (name/desc overrides)
# ============================================================================

# Glands
ent-ImplantDubiousBase = сомнительная железа
    .desc = Микроскопическая инопланетная железа, которую можно вживить под кожу подопытного.
ent-ImplantDubiousHealth = сомнительная железа
    .desc = Микроскопическая инопланетная железа, которую можно вживить под кожу подопытного.
ent-ImplantDubiousAA = сомнительная железа
    .desc = Микроскопическая инопланетная железа, которую можно вживить под кожу подопытного.
ent-ImplantDubiousShock = сомнительная железа
    .desc = Микроскопическая инопланетная железа, которую можно вживить под кожу подопытного.
ent-ImplantDubiousInvisible = сомнительная железа
    .desc = Микроскопическая инопланетная железа, которую можно вживить под кожу подопытного.
ent-ImplantDubiousArtifact = сомнительная железа
    .desc = Микроскопическая инопланетная железа, которую можно вживить под кожу подопытного.
ent-ImplantDubiousRepairable = сомнительная железа
    .desc = Микроскопическая инопланетная железа, которую можно вживить под кожу подопытного.

# Implanters (syringes)
ent-ImplanterDubiousHealth = сомнительный имплантатор здоровья
    .desc = Одноразовый шприц с железой здоровья. Выбирайте подопытных с умом.
ent-ImplanterDubiousAA = сомнительный имплантатор доступа
    .desc = Одноразовый шприц с железой доступа. Выбирайте подопытных с умом.
ent-ImplanterDubiousShock = сомнительный имплантатор шока
    .desc = Одноразовый шприц с железой шока. Выбирайте подопытных с умом.
ent-ImplanterDubiousInvisible = сомнительный имплантатор невидимости
    .desc = Одноразовый шприц с железой невидимости. Выбирайте подопытных с умом.
ent-ImplanterDubiousArtifact = сомнительный имплантатор артефактов
    .desc = Одноразовый шприц с железой артефактов. Выбирайте подопытных с умом.
ent-ImplanterDubiousRepairable = сомнительный имплантатор ремонта
    .desc = Одноразовый шприц с железой ремонта. Выбирайте подопытных с умом.

# Spawner
ent-DubiousOrganSpawner = Спавнер сомнительных желёз

# Mobs
ent-MobAbductor = абдуктор
ent-MobLoneAbductor = абдуктор-одиночка
ent-MobAbductorScientist = учёный-абдуктор
ent-MobAbductorAgent = агент-абдуктор

# Spawn points
ent-SpawnPointLoneAbductor = спавнер абдуктора-одиночки
ent-SpawnPointAbductorAgent = спавнер агента-абдуктора
ent-SpawnPointAbductorScientist = спавнер учёного-абдуктора

# Weapons / projectiles
ent-BaseBulletStarlight = Базовая пуля
    .desc = Если ты видишь это сообщение — ты, скорее всего, мёртв!
ent-BulletDeclone = заряд деклонера
ent-WeaponAlien = инопланетный пистолет
    .desc = Это военное преступление? Скорее всего.
ent-WeaponDecloner = деклонер
    .desc = Это военное преступление? Однозначно.
ent-Silencer = глушитель
    .desc = Инструмент для лишения людей дара речи.
ent-Wonderprod = чудо-жезл
    .desc = Универсальный инструмент агента-абдуктора.
ent-AbductorGizmo = гизмо
    .desc = Устройство, вживляющее нано-маячок. Его можно использовать, чтобы навести телепортационный луч.

# Clothing
ent-ClothingUniformJumpsuitAbductor = форма абдуктора
ent-ClothingBackpackAbductor = инопланетный рюкзак
    .desc = Обычный рюкзак, сплетённый из прочного волокна.
ent-ClothingBackpackDuffelAbductor = инопланетная сумка
    .desc = Обычная сумка, сплетённая из прочного волокна.
ent-ClothingHeadsetAltAbductor = инопланетная наушная гарнитура
ent-ClothingHeadHelmetAbductor = инопланетный шлем
    .desc = Зип Глорп!
ent-ClothingOuterArmorAbductor = жилет агента
ent-ClothingOuterCoatAbductor = инопланетный лабораторный халат
    .desc = Костюм инопланетного происхождения, защищающий от травм и химических разливов.
ent-ClothingAbductorBelt = инопланетный пояс
    .desc = Вмещает различные НАШИ вещи.
ent-ClothingBackpackDuffelAbductorFilled = инопланетная хирургическая сумка
    .desc = Обычная хирургическая сумка, сплетённая из прочного волокна.

# Machines / structures
ent-AbductorExperimentator = экспериментатор
    .desc = Устройство, анализирующее результат эксперимента и возвращающее подопытного туда, откуда его забрали.
ent-AbductorAlienPad = инопланетная площадка
    .desc = Притяните низшие формы жизни и приступайте к делу.
ent-AbductorConsole = консоль абдукторов
    .desc = Компьютер, используемый для шпионажа за станцией.
ent-AbductorHumanObservationConsole = консоль наблюдения за людьми
    .desc = Используйте, чтобы задать точку телепортации или вернуть людей, помеченных инструментами учёного. Также позволяет закупать снаряжение и привязывать жилет агента.
ent-AbductorHumanObservationConsoleEye = глаз абдуктора
    .desc = Взор абдуктора.
ent-AbductorOperatingTable = операционный стол абдукторов
ent-TableAbductor = инопланетный стол
    .desc = Самая прочная штука, которую вы когда-либо видели.
ent-WallAbductor = стена шаттла
    .desc = Держит воздух внутри, а кровожадных утилизаторов — снаружи.
ent-WallAbductorDiagonal = стена шаттла
    .desc = Держит воздух внутри, а кровожадных утилизаторов — снаружи.

# Actions
ent-ActionExitConsole = Покинуть консоль
    .desc = Покинуть консоль.
ent-ActionSendYourself = Телепортировать себя
    .desc = Чтобы использовать эту способность, совершите БПД на ту же карту, что и станция!
ent-ActionReturnToShip = вернуться
    .desc = вернуться на корабль.

# Mind roles
ent-MindRoleLoneAbductor = Роль: абдуктор-одиночка
ent-MindRoleAbductorAgent = Роль: агент-абдуктор
ent-MindRoleAbductorScientist = Роль: учёный-абдуктор
ent-MindRoleAbductorVictim = Роль: жертва похищения

# ============================================================================
# Abductor victim objectives
# ============================================================================

ent-AbductorVictimPaintObjective = Раскрась станцию.
    .desc = Станция уродлива. Ты обязан раскрасить её всю!
ent-AbductorVictimPristine = Обеспечь безупречность станции.
    .desc = Приезжает руководитель Nanotrasen! Станция должна быть в абсолютно идеальном состоянии.
ent-AbductorVictimBlingFloor = Замени пол.
    .desc = Замени все плитки пола на дерево, ковёр, траву или блестящее покрытие.
ent-AbductorVictimCorpseCollector = Собирай трупы.
    .desc = Начни коллекцию трупов. Не убивай людей ради этой коллекции.
ent-AbductorVictimParaplegic = Паралитик.
    .desc = Убеди экипаж, что ты паралитик.
ent-AbductorVictimHungry = Утоли голод.
    .desc = Ты голоден. Съешь как можно больше еды.
ent-AbductorVictimBlazeIt = Химически улучши своё тело.
    .desc = Твоё тело нужно улучшить. Прими как можно больше препаратов.
ent-AbductorVictimSocialExperiment = Это всё ложь.
    .desc = Это секретный социальный эксперимент, проводимый Nanotrasen. Убеди экипаж, что это правда.
ent-AbductorVictimVirtualInsanity = НИЧТО НЕ РЕАЛЬНО.
    .desc = Всё это — полностью виртуальная симуляция в подземном бункере. Убеди экипаж сбежать от оков VR.
ent-AbductorVictimGame = Чат, это реально?
    .desc = Убеди экипаж, что мы в игре, не говоря им прямо, что мы в игре.
ent-AbductorVictimSaveAnimals = Спаси животных.
    .desc = Nanotrasen издевается над животными! Спаси как можно больше!
ent-AbductorVictimMusic = Поделись своей музыкой.
    .desc = Ты горишь страстью к музыке. Поделись своим видением. Если кому-то не понравится — бей их по голове своим инструментом!
ent-AbductorVictimStalker = Следи за экипажем.
    .desc = Кто-то нанял тебя составить досье на всех важных членов экипажа. Смотри, чтобы они не узнали, чем ты занят.
ent-AbductorVictimConspiracy = Заговор.
    .desc = Лидеры этой станции скрывают грандиозный злой заговор. Только ты можешь узнать его и выставить на всеобщее обозрение!
ent-AbductorVictimNarrator = Рассказывай историю.
    .desc = Ты рассказчик этой истории. Следуй за главными героями и повествуй их историю.
ent-AbductorVictimSixthsense = Ты вообще жив?
    .desc = Ты умер и попал в рай... или в ад? Никто здесь, похоже, не знает, что он мёртв. Убеди их — и, может быть, ты сбежишь из этого лимба.
ent-AbductorVictimParty = ВЕЧЕРИНКА!
    .desc = Тебе НУЖНО устроить огромную тусовку. Сделай её максимально крутой, чтобы пришёл весь экипаж... ИЛИ ЖЕ!
ent-AbductorVictimDeadStationTheory = Существует только один человек.
    .desc = В мире есть только один человек, просто он очень хорошо притворяется несколькими.
ent-AbductorVictimWeaknessOfMyFlesh = Плоть гниёт.
    .desc = Твоя плоть гниёт прямо на теле. Борись с неизбежным и замени прогнившие конечности целиком на новые.
ent-AbductorVictimInstructor = Военный инструктор.
    .desc = Ты военный инструктор. Ты должен привести экипаж в отличную форму к войне с синдикатом!
ent-AbductorVictimActor = Боевик.
    .desc = Ты в боевике. Ты должен произносить как можно больше пафосных односложных фраз.
ent-AbductorVictimNations = Прославь свой отдел.
    .desc = Сделай так, чтобы твой отдел процветал больше всего.
ent-AbductorVictimPowerUnlimited = Безграничная мощь.
    .desc = Залей электросеть станции как можно большим количеством электричества.
ent-AbductorVictimBuild = Расширь станцию.
    .desc = Станция слишком маленькая, и ты это исправишь.
ent-AbductorVictimDefect = Сбеги со станции.
    .desc = Плевать на систему! Покинь станцию и начни независимую колонию в космосе, на шахтёрском аванпосте или в заброшенном корпусе. По возможности набери союзников из экипажа.
ent-AbductorVictimMonkeyTakeover = Собери армию обезьян.
    .desc = УК УК УАА!!!
ent-AbductorVictimAverageFrontierPirate = Воруй обувь.
    .desc = Скради как можно больше обуви.
ent-AbductorVictimNoClown = Клоун не смешной.
    .desc = Клоун не смешной. У тебя получится лучше! Займи его роль и заставь экипаж смеяться!
ent-AbductorVictimReplacePets = Замени питомцев.
    .desc = Все здешние питомцы — отстой. Нужно сделать их круче. Замени их экзотическими зверями!
ent-AbductorVictimBald = Спаси людей от вторжения волос.
    .desc = Инопланетные паразиты маскируются под волосы людей. Спаси людей от этого вторжения.
ent-AbductorVictimNoWalls = Снеси стены.
    .desc = Экипажу нужно лучше узнать друг друга. Снеси стены внутри станции!
ent-AbductorVictimAbductception = Отомсти.
    .desc = Ты изменился навсегда. Найди инопланетян, которые это сделали, и угости их их же лекарством.
ent-AbductorVictimNoOxygen = КИСЛОРОД УБИВАЕТ ВСЕХ.
    .desc = Кислород убивает их всех, а они даже не знают. Сделай так, чтобы на станции не осталось кислорода.
ent-AbductorVictimEscapeStation = Сбеги со станции.
    .desc = Ты должен сбежать со станции! Вызови шаттл!
ent-AbductorVictimNoCloning = Никакого клонирования.
    .desc = Не позволяй никому быть клонированным.
ent-AbductorVictimStealWeapons = Кради оружие.
    .desc = Скради столько оружия, сколько сможешь унести на себе.
ent-AbductorVictimDismantleComputers = Разбери компьютеры.
    .desc = Волны 7G от компьютеров убивают экипаж, а они не знают об этом! Разбери их!
ent-AbductorVictimFinality = Без воскрешения.
    .desc = Смерть должна быть окончательной, а современная медицина нарушает естественный порядок. Не позволяй никого оживить.

# ============================================================================
# Abductor datasets
# ============================================================================

abductor-names-dataset-1 = Zxi'ra
abductor-names-dataset-2 = Qor'tlan
abductor-names-dataset-3 = N'voth
abductor-names-dataset-4 = Krath'mir
abductor-names-dataset-5 = Ul'zex
abductor-names-dataset-6 = Vees'kat
abductor-names-dataset-7 = Zorp'lin
abductor-names-dataset-8 = Xul'thax
abductor-names-dataset-9 = M'lorq
abductor-names-dataset-10 = Grah'ss
abductor-names-dataset-11 = Teek'zo
abductor-names-dataset-12 = F'naril
abductor-names-dataset-13 = Skith`ven
abductor-names-dataset-14 = Y'gral
abductor-names-dataset-15 = Ool'thub
abductor-names-dataset-16 = Rixe'nor
abductor-names-dataset-17 = Zh'quail
abductor-names-dataset-18 = P'laxt
abductor-names-dataset-19 = Dr'zrak
abductor-names-dataset-20 = T'venk
abductor-names-dataset-21 = So'thiss
abductor-names-dataset-22 = Mlax'zor
abductor-names-dataset-23 = K'zith
abductor-names-dataset-24 = Egy'rith
abductor-names-dataset-25 = Nor'gath
abductor-names-dataset-26 = Y'vess
abductor-names-dataset-27 = Um'thor
abductor-names-dataset-28 = Skal'than
abductor-names-dataset-29 = Oz'rek
abductor-names-dataset-30 = P'ritha
abductor-names-dataset-31 = Zluk'bar
abductor-names-dataset-32 = Hen'xaw
abductor-names-dataset-33 = Q'gmoth
abductor-names-dataset-34 = Vrax'yn
abductor-names-dataset-35 = Tu'orlash
abductor-names-dataset-36 = Syff'ra
abductor-names-dataset-37 = Mlu'hn
abductor-names-dataset-38 = Gr'thax
abductor-names-dataset-39 = Xenn'ura
abductor-names-dataset-40 = D'zakos
abductor-names-dataset-41 = Wl'feth
abductor-names-dataset-42 = Kar'suma
abductor-names-dataset-43 = R'ksoth
abductor-names-dataset-44 = Ny'zhul
abductor-names-dataset-45 = Saeg'loun

abductor-scientist-prefix-dataset-1 = Zkorath
abductor-scientist-prefix-dataset-2 = Qex'lin

abductor-agent-prefix-dataset-1 = Nith'hix
abductor-agent-prefix-dataset-2 = Vrz'gul