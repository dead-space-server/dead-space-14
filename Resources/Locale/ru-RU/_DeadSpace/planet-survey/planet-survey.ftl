survey-beacon-landing = { $planet }: зона высадки
survey-beacon-far = { $planet }: дальний склон
survey-cargo-examine = [color=#7fb8ff]Груз экспедиции.[/color] Не открывается: ценен целиком. { $need }
survey-cargo-examine-reward = Если довезти на станцию: { $money } кредитов и { $items }.
survey-cargo-examine-money = Если довезти на станцию: { $money } кредитов.
survey-cargo-examine-stolen = [color=#ffb347]Украдено со станции. Ценен, только если довезти без повреждений.[/color]
survey-search-start = Сканер слушает пустоту. Поиск займёт время и может ничего не дать.
survey-search-busy = Поиск уже идёт. Ещё около { $seconds } с.
survey-search-mapping = Планета найдена, идёт картография.
survey-search-known = Планета уже найдена: { $planet }. Её маяки на карте секторов.
survey-search-nothing = Ничего. Только шум. Можно попробовать ещё раз.

survey-sender = Навигация экспедиции
survey-sender-sky = Планета
survey-found = Найдена планета { $planet }. { $land } { $motive } Ценных грузов внизу: { $cargo }. Нажмите «Сканировать» в консоли шаттла: на карте появятся маяки высадки.

survey-motive-retrieve = Внизу груз, украденный со станции. Оплата только если довезти целым.
survey-motive-windfall = Внизу чужой груз. Можно оставить, можно увезти за деньги.
survey-motive-curse = Внизу чужой груз. Можно оставить, можно увезти за деньги.

survey-land-city = Развалины города.
survey-land-cove = Лагерь у воды.
survey-land-factory = Цеха ещё печатают формы.
survey-land-forest = Лес.
survey-land-jungle = Кислотный дождь.
survey-land-tundra = Открытая тундра.
survey-land-frost = Лёд и пустые постройки.
survey-land-dust = Пыль до горизонта.

survey-storm-warning-first = Небо темнеет. Скоро буря, ищите крышу.
survey-storm-warning = Буря снова. Эта сильнее прошлой.
survey-storm-over = Буря кончилась. Следующая будет.

survey-leave-empty = Улетели пустыми. Зато живые.
survey-leave-cargo = Увезли с планеты: { $count } { $count ->
    [one] груз
    [few] груза
   *[other] грузов
}. { $heat ->
    [0] Счёт пока небольшой.
   *[other] Счёт растёт.
}

survey-cargo-stolen = { $name } (украдено со станции)

survey-deliver-retrieve = Экспедиция вернула { $name }. Груз цел. { $need } Станции начислено { $money } кредитов.
survey-deliver-broken = Экспедиция привезла { $name }, но груз повреждён и бесполезен.
survey-deliver-windfall = Экспедиция привезла { $name }. { $need } Станции начислено { $money } кредитов.

survey-curse-beasts = За { $name } пришла стая. Они уже на станции.
survey-curse-raiders = За { $name } прилетели те, у кого его забрали. Они на станции.
survey-curse-pirates = Пираты выследили { $name }. Они на борту.
survey-curse-factory = { $name } снова печатает. Формы лезут из него.
survey-curse-deadelement = { $name } фонит. Вместе с ним на станцию пришла мёртвая земля.

survey-need-science = Накопитель с дисками. Науке, целиком.
survey-need-command = Ядро чертежа. Командованию, на месте не вскрывать.
survey-need-engineering = Микрореактор. Инженерам, тёплый даже выключенный.
survey-need-medical = Ампулы без маркировки. Медикам, на себе не пробовать.
survey-need-security = Опечатанный кейс. Охране, пломбу не срывать.
survey-need-cargo = Ящик со слитками. Снабжению, продаётся как есть.

cmd-surveyplanet-desc = Создаёт планету экспедиции этого раунда.
cmd-surveyplanet-help = surveyplanet [мир|*] [повод|*] [заменить true|false]. Звёздочка: случайно. С заменой текущая планета удаляется, если на ней никого нет.
cmd-surveyplanet-occupied = На текущей планете есть игроки или призраки ({ $count }). Сначала уведите их.
cmd-surveytp-desc = Переносит вас на планету экспедиции.
cmd-surveytp-help = surveytp [landing|town|far]: зона высадки, край поселения или дальний склон.
cmd-surveytp-none = Планеты нет или она ещё создаётся.
cmd-surveytp-done = Вы на планете { $planet }.
cmd-surveyplanet-bad-world = Нет такого мира.
cmd-surveyplanet-bad-motive = Повод: Retrieve, Windfall или Curse.
cmd-surveyplanet-done = Планета готова: { $planet }
cmd-surveyplanet-failed = Планету создать не удалось.

survey-bark-raider-seen-1 = Чужие! К оружию!
survey-bark-raider-seen-2 = Гости пожаловали. Встречаем.
survey-bark-raider-seen-3 = Вижу их! Не высовывайтесь!
survey-bark-raider-seen-4 = Опять станционные. Ну давайте.
survey-bark-raider-hurt-1 = А-а, задело!
survey-bark-raider-hurt-2 = Нога! Не могу!
survey-bark-raider-hurt-3 = Я ранен, прикройте!
survey-bark-raider-loss-1 = Нет... Его больше нет.
survey-bark-raider-loss-2 = Они убили нашего!
survey-bark-raider-loss-3 = Брат, держись... Поздно.
survey-bark-raider-kill-1 = Готов!
survey-bark-raider-kill-2 = Один есть!
survey-bark-raider-kill-3 = Так им!
survey-bark-raider-retreat-1 = Отходим! Отходим!
survey-bark-raider-retreat-2 = Нас мало, назад!
survey-bark-raider-retreat-3 = Уходим в развалины!
survey-bark-raider-idle-1 = Воды бы чистой.
survey-bark-raider-idle-2 = Опять дождь.
survey-bark-raider-idle-3 = Слышал? Нет, показалось.
survey-bark-raider-idle-4 = Кто сегодня в дозоре?
survey-bark-raider-loot-1 = Они тащат наше! Держи вора!
survey-bark-raider-loot-2 = Груз уходит! За ними!
survey-bark-raider-revenge-1 = Думали, не найдём?
survey-bark-raider-revenge-2 = Вы забрали наше. Пришли за долгом.

survey-bark-pirate-seen-1 = Глядите-ка, кто к нам пожаловал.
survey-bark-pirate-seen-2 = Свистать всех!
survey-bark-pirate-seen-3 = Цель на горизонте.
survey-bark-pirate-deal-1 = Стоять. Положите груз у нашего сундука, и разойдёмся миром.
survey-bark-pirate-deal-2 = Пошлина за проход. Один ящик к сундуку, и никто не умрёт.
survey-bark-pirate-deal-3 = Торговаться будем или стреляться? Дань к сундуку.
survey-bark-pirate-paid-1 = Вот это разговор. Проходите.
survey-bark-pirate-paid-2 = Сделка есть сделка. Пока что.
survey-bark-pirate-betray-1 = Передумали. Забираем всё!
survey-bark-pirate-betray-2 = Сделка кончилась. Огонь!
survey-bark-pirate-betray-3 = Мёртвые торгуются дешевле!
survey-bark-pirate-hurt-1 = Меня зацепило!
survey-bark-pirate-hurt-2 = Чёрт, рука!
survey-bark-pirate-loss-1 = Ещё одного за борт...
survey-bark-pirate-loss-2 = Они дорого заплатят!
survey-bark-pirate-kill-1 = Одним меньше!
survey-bark-pirate-kill-2 = На дно его!
survey-bark-pirate-retreat-1 = Кровь дороже добычи! Уходим!
survey-bark-pirate-retreat-2 = Не стоит оно того. Назад!
survey-bark-pirate-idle-1 = Делёж в полночь.
survey-bark-pirate-idle-2 = Кто брал мой ром?
survey-bark-pirate-idle-3 = Скоро придёт корабль. Будем богаты.
survey-bark-pirate-loot-1 = Добыча уходит! За ней!
survey-bark-pirate-revenge-1 = Вы увезли наше. Мы пришли за своим.
survey-bark-pirate-revenge-2 = Пираты долги не прощают.

survey-ui-title = Экспедиция: { $planet }
survey-ui-threat-low = На планете тихо
survey-ui-threat-mid = На планете стреляют редко
survey-ui-threat-high = На планете стреляют

survey-obj-explore = Исследовать планету: найдено мест { $done }/{ $goal }
survey-obj-retrieve = Найти и погрузить на шаттл украденное: { $done }/{ $goal }
survey-obj-cargo = Погрузить ценный груз на шаттл: { $done }/{ $goal }
survey-obj-raiders = Сломить рейдеров: выбыло { $done }/{ $goal }
survey-obj-pirates = Разобраться с пиратами: заплатить дань у сундука или разбить их
survey-obj-stamps = Взорвать штампы, чтобы завод перестал печатать: { $done }/{ $goal }
survey-obj-dens = Изучить кладки выводка у логов: { $done }/{ $goal }
survey-obj-storm = Пережить бурю в укрытии без потерь
survey-obj-study = Изучить аномальные объекты (лучше со сканером): { $done }/{ $goal }
survey-obj-done = Задача выполнена: { $text } Станция и экипаж получили награду.

survey-personal-share = Ваша доля за груз: { $money } кредитов.

survey-verb-study = Изучить
survey-study-start = Вы начинаете изучать находку.
survey-study-done = Данные ушли на сервер НИО: { $points } очков. Вам причитается премия.

survey-relic-bloom = Раны стягиваются. Под кожей что-то шевельнулось.
survey-relic-bloom-wither = Цветок осыпается пеплом.
survey-relic-hatch = Яйцо лопается!
survey-relic-egg-kick = Яйцо дёргается в руках.
survey-relic-whisper-1 = Шар шепчет чьё-то имя.
survey-relic-whisper-2 = Из шара тянет холодом. Кто-то идёт на зов.
survey-relic-whisper-3 = Шар тихо смеётся.
survey-relic-whisper-4 = Шар просит, чтобы его не продавали.

survey-bark-raider-reinforce-1 = Подмога пришла! Зажимай их!
survey-bark-raider-reinforce-2 = Мы с тыла! Не дайте им уйти!
survey-bark-pirate-reinforce-1 = Абордажная команда на месте!
survey-bark-pirate-reinforce-2 = Окружай, ребята!

id-card-access-level-survey-native = Жители планеты

survey-verb-sabotage-power = Перерезать питание
survey-verb-sabotage-comms = Вывести мачту из строя
survey-sabotage-done = Готово. На базе это заметят не сразу.
survey-ui-spotted = Вас заметили! Тревога через { $seconds } с, если дозорные выживут
survey-ui-alarm = ТРЕВОГА: база поднята на ноги
survey-ui-blackout = база обесточена
survey-ui-comms = связи у них нет
survey-obj-power = (по желанию) Обесточить главную базу через энергоузел на форпосте
survey-obj-comms = (по желанию) Вывести из строя мачту связи: тревога не разойдётся
survey-obj-stealth = (по желанию) Взять форпост, не подняв тревоги

survey-bark-raider-alarm-1 = Чужие! Поднимай всех!
survey-bark-raider-alarm-2 = Тревога! Зови базу!
survey-bark-raider-blackout-1 = Свет погас! Кто на узле?!
survey-bark-raider-blackout-2 = Темно, как в могиле. Не стрелять по своим!
survey-bark-raider-nocomms-1 = База не отвечает!
survey-bark-raider-nocomms-2 = Связи нет, держимся сами!
survey-bark-pirate-alarm-1 = Бей в колокол! Все наверх!
survey-bark-pirate-alarm-2 = Гости! Буди команду!
survey-bark-pirate-blackout-1 = Кто погасил огни?!
survey-bark-pirate-nocomms-1 = Мачта молчит! Мы одни!
survey-ui-deal = Пираты держат огонь и ждут дань: груз к их сундуку. Осталось { $seconds } с

survey-core-greet = ЯДРО: Обнаружена органика. Сырьё принято к переработке.
survey-core-down = Ядро завода погасло. Цех встал, формы падают замертво.
survey-obj-core = Уничтожить ядро завода в центральном цехе: всё остальное встанет

survey-bark-raider-help-1 = Меня прижали! Нужна помощь!
survey-bark-raider-help-2 = Всем, кто слышит: стреляют у нас! Подтягивайтесь!
survey-bark-raider-help-3 = Сюда! Их больше, чем казалось!
survey-bark-raider-qrf-1 = Отряд не отвечает. Высылаю подмогу!
survey-bark-raider-qrf-2 = Наших положили! Поднимайте тяжёлых!
survey-bark-pirate-help-1 = Нас зажали! Все сюда!
survey-bark-pirate-help-2 = Кто живой, подтягивайся!
survey-bark-pirate-qrf-1 = Шлюпка молчит. Буди абордажников!
survey-bark-pirate-qrf-2 = Наших режут! Тащите стволы получше!
survey-bark-raider-reinforce-3 = Идём! Держитесь!
survey-bark-pirate-reinforce-3 = Уже бежим, не сдохни!

survey-bark-raider-medic-1 = Держись, иду к тебе!
survey-bark-raider-medic-2 = Медик на месте, не дёргайся!
survey-bark-raider-medic-3 = Прикройте, лезу за раненым!
survey-bark-raider-drag-1 = Тащу его в укрытие! Огонь на подавление!
survey-bark-raider-drag-2 = Хватаю! Прикройте отход!
survey-bark-raider-patch-1 = Жгут наложил, дыши!
survey-bark-raider-patch-2 = Ещё немного, не отключайся!
survey-bark-raider-grenade-1 = Граната!
survey-bark-raider-grenade-2 = Ловите подарочек!
survey-bark-raider-grenade-3 = Выкуриваю их!
survey-bark-raider-smoke-1 = Дым! Уходим под дымом!
survey-bark-raider-smoke-2 = Ставлю завесу!
survey-bark-raider-scout-1 = Дрон их видит! Все туда!
survey-bark-raider-scout-2 = Птичка засекла чужих, координаты есть!
survey-bark-pirate-medic-1 = Лекарь идёт, не помирай!
survey-bark-pirate-medic-2 = Держись, сейчас заштопаю!
survey-bark-pirate-drag-1 = Тащу за борт, прикройте!
survey-bark-pirate-drag-2 = Беру его, держите их на прицеле!
survey-bark-pirate-patch-1 = Терпи, ещё стежок!
survey-bark-pirate-patch-2 = Залатал, живи!
survey-bark-pirate-grenade-1 = Осколочная!
survey-bark-pirate-grenade-2 = Получайте, крысы!
survey-bark-pirate-smoke-1 = Дым! Отходим!
survey-bark-pirate-smoke-2 = Завеса, все назад!
survey-bark-pirate-scout-1 = Глазок их заметил! Все туда!
survey-bark-pirate-scout-2 = Дрон показал гостей, встречаем!

chat-radio-survey-native = Туземная частота

survey-place-landing = у посадочной площадки
survey-place-far = у дальнего маяка
survey-place-power = у энергоузла
survey-place-comms = у мачты связи
survey-place-outpost = у форпоста
survey-place-stamp = у штампа
survey-place-device = у узла управления
survey-place-site = у странной находки
survey-place-den = у логова
survey-place-settlement = в посёлке
survey-place-base = на базе, { $dir } крыло
survey-place-sector = в { $dir } секторе

survey-sector-north = северном
survey-sector-south = южном
survey-sector-east = восточном
survey-sector-west = западном
survey-sector-centre = центральном
survey-heading-north = идут на север
survey-heading-south = идут на юг
survey-heading-east = идут на восток
survey-heading-west = идут на запад
survey-heading-centre = стоят на месте
survey-heading-still = стоят на месте

survey-radio-raider-sitrep-1 = { $where }: вижу { $count }, { $heading }.
survey-radio-raider-sitrep-2 = Пост, гости { $where }. { $heading }. Насчитал { $count }.
survey-radio-raider-sitrep-3 = { $count } у { $where }. { $heading }. Не зевайте.
survey-radio-raider-sitrep-4 = Чужие { $where }, { $heading }. Кто ближе, берите их.
survey-radio-raider-idle-1 = { $where }, тихо. Даже собаки не лают.
survey-radio-raider-idle-2 = Кто опять снял батареи со склада? Это { $where }.
survey-radio-raider-idle-3 = На { $where } пусто. Жду смену.
survey-radio-raider-idle-4 = { $where } на месте. Кормёжку так и не принесли.
survey-radio-raider-contact-1 = Стреляю, { $where }. Их { $count }.
survey-radio-raider-contact-2 = { $count } рыл { $where }, не спят.
survey-radio-raider-contact-3 = { $where }, к бою. Гости уже тут.
survey-radio-raider-help-1 = { $where }, нас прижали. Кто свободен?
survey-radio-raider-help-2 = { $where }, двоих уже нет. Подходите.
survey-radio-raider-moving-1 = Иду на { $where }.
survey-radio-raider-moving-2 = Принял, { $where }. Держитесь, мы близко.
survey-radio-raider-qrf-1 = С { $where } никто не отвечает. Поднимаю тяжёлых.
survey-radio-raider-qrf-2 = { $where } замолчали. Шлю вторую группу.
survey-radio-raider-arrive-1 = Мы у { $where }. Заходим.
survey-radio-raider-arrive-2 = Тяжёлые на { $where }.
survey-radio-raider-alarm-1 = Тревога. Чужие с { $where }. Броню надели?
survey-radio-raider-alarm-2 = Общая. Началось { $where }. Оружейка открыта.
survey-radio-raider-scout-1 = Дрон: { $count } { $where }. Перехват.
survey-radio-raider-scout-2 = Птичка видит { $count } { $where }. Кто рядом, не спите.
survey-radio-raider-medic-1 = Раненый { $where }, иду.
survey-radio-raider-medic-2 = Медик на { $where }. Прикройте коридор.
survey-radio-raider-blackout-1 = Свет сел. Кто у кабелей?
survey-radio-raider-blackout-2 = Энергоузел молчит. Фонари.

survey-radio-pirate-sitrep-1 = С мостика: { $count } { $where }, { $heading }.
survey-radio-pirate-sitrep-2 = Вижу их { $where }. { $heading }. Штук { $count }.
survey-radio-pirate-sitrep-3 = Капитан, { $where }. { $heading }. Не отпускайте к шаттлу.
survey-radio-pirate-idle-1 = Вахта { $where }. Вода тихая.
survey-radio-pirate-idle-2 = Кто лазил в мой ящик? Вахта { $where }.
survey-radio-pirate-idle-3 = { $where } пусто. Чайки и те не орут.
survey-radio-pirate-contact-1 = Стреляем, { $where }. Их { $count }.
survey-radio-pirate-contact-2 = { $count } голов { $where }. Не торгуемся.
survey-radio-pirate-help-1 = { $where }, нас меньше. Все сюда.
survey-radio-pirate-help-2 = { $where } жжёт. Кто на ногах, бегом.
survey-radio-pirate-moving-1 = Иду на { $where }.
survey-radio-pirate-moving-2 = Понял. Заходим с { $where }.
survey-radio-pirate-qrf-1 = Вахта { $where } молчит. Будите абордаж.
survey-radio-pirate-qrf-2 = С { $where } никого не осталось. Шлю запасных.
survey-radio-pirate-arrive-1 = Абордаж на { $where }.
survey-radio-pirate-alarm-1 = Все наверх. Чужие у { $where }.
survey-radio-pirate-alarm-2 = Тревога с { $where }. Сундук не бросать.
survey-radio-pirate-scout-1 = Глазок: { $count } { $where }.
survey-radio-pirate-scout-2 = Дрон нашёл их { $where }, { $count }.
survey-radio-pirate-medic-1 = Лекарь идёт на { $where }. Не дёргайся.
survey-radio-pirate-blackout-1 = Свет погас. Смотрите трюм, не небо.

survey-radio-factory-sitrep-1 = Цели: { $count }. Точка { $where }. Движение: { $heading }.
survey-radio-factory-sitrep-2 = Сенсор { $where }. Идут: { $heading }.
survey-radio-factory-idle-1 = Линия { $where } стоит. План не закрыт.
survey-radio-factory-idle-2 = { $where }: штамп ждёт заготовку.
survey-radio-factory-contact-1 = Стрельба { $where }. Целей { $count }.
survey-radio-factory-contact-2 = Периметр { $where } открыт. Формы уже идут.
survey-radio-factory-qrf-1 = Группа { $where } не отвечает. Печатаю замену.
survey-radio-factory-arrive-1 = Замена вышла к { $where }.
survey-radio-factory-alarm-1 = Тревога. Очаг { $where }. Линии не останавливать.
survey-radio-factory-scout-1 = Наблюдатель: { $count } у { $where }. Координаты ушли.
survey-radio-factory-blackout-1 = Питание село. Режу лишние линии, штампы живы.

survey-bark-raider-incoming-1 = Граната! Ложись!
survey-bark-raider-incoming-2 = В сторону! Граната!
survey-bark-raider-incoming-3 = Сейчас рванёт!
survey-bark-raider-selfheal-1 = Зацепило... сейчас перевяжусь.
survey-bark-raider-selfheal-2 = Прикройте, латаюсь!
survey-bark-raider-stim-1 = Колю стим!
survey-bark-raider-stim-2 = Ух, пошло по венам!
survey-bark-raider-stim-3 = Теперь я злой!
survey-bark-pirate-incoming-1 = Граната! За борт!
survey-bark-pirate-incoming-2 = Разбегайся!
survey-bark-pirate-selfheal-1 = Дай минуту, заштопаюсь.
survey-bark-pirate-selfheal-2 = Царапина! Сейчас перевяжу.
survey-bark-pirate-stim-1 = За капитана! Колю!
survey-bark-pirate-stim-2 = Вот это зелье!

survey-radio-raider-crit-1 = Пацаны... я маслину словил... { $where }...
survey-radio-raider-crit-2 = Меня зацепило... лежу { $where }... заберите, а?
survey-radio-raider-crit-3 = Братва... холодно что-то стало...
survey-radio-raider-crit-4 = Кровью истекаю { $where }! Медика! Хоть кого-нибудь!
survey-radio-raider-crit-5 = Мужики, если что... заначка под третьей бочкой...
survey-radio-pirate-crit-1 = Тысяча чертей... меня продырявили { $where }...
survey-radio-pirate-crit-2 = Помираю { $where }... кто-нибудь, рому...
survey-radio-pirate-crit-3 = Капитан... я своё отплавал...

survey-radio-raider-laugh-1 = Гы-гы. Старый, но годный.
survey-radio-raider-laugh-2 = Ещё раз этот расскажешь - лично пристрелю.
survey-radio-raider-laugh-3 = Ха! Колян, это ж ты про себя.
survey-radio-raider-laugh-4 = Эфир не засоряйте, юмористы.
survey-radio-pirate-laugh-1 = Боцман слышал. Доплаты за это не будет.
survey-radio-pirate-laugh-2 = За такие шутки - за борт!
survey-radio-factory-laugh-1 = Записано. Линия из-за этого не встанет.

survey-joke-raider-1-1 = Колян поймал станционного с ящиком.
survey-joke-raider-1-2 = Тот орёт, что ящик научный и его нельзя ронять.
survey-joke-raider-1-3 = Колян уронил. Теперь у нас ящик и претензия.
survey-joke-raider-2-1 = Приходит новенький к старому: «Научи, как тут выжить?»
survey-joke-raider-2-2 = Старый: «Видишь вон ту ловушку?» - «Нет». - «Вот и я не вижу. А она есть».
survey-joke-raider-3-1 = Командир спрашивает часового: «Почему на посту спал?»
survey-joke-raider-3-2 = «Не спал, я врага в темноте высматривал». - «И как?» - «Хорошо маскируется, гад».
survey-joke-raider-4-1 = Встречает наш пирата: «Ты зачем повязку на глаз носишь?» - «Целиться удобнее».
survey-joke-raider-4-2 = «А попугай зачем?» - «А он подсказывает, куда».
survey-joke-raider-5-1 = Новенький спрашивает: «А тут платят?»
survey-joke-raider-5-2 = Старый: «Платят. Кто патронами, кто шкурой - как повезёт».
survey-joke-raider-6-1 = Новенький спрятался от смены в вентиляции.
survey-joke-raider-6-2 = Нашли к ужину. Колян сказал, в следующий раз пусть прячется на кухне, там хоть кормят.
survey-joke-raider-7-1 = Заходит станционный шаттл на посадку. Пилот по связи: «Земля, где у вас тут площадка?»
survey-joke-raider-7-2 = А ему с вышки: «Садись куда хочешь, браток. Мы потом сами подберём».
survey-joke-raider-8-1 = Колян вчера нашёл ящик с надписью «Не открывать».
survey-joke-raider-8-2 = Ну, открыл. Теперь у нас нет Коляна, зато есть ящик.
survey-joke-raider-9-1 = Часовой клянётся, что пустошь на него смотрит.
survey-joke-raider-9-2 = Командир посмотрел в бинокль. Это была коза. Часового с поста сняли.

survey-joke-pirate-1-1 = Юнга третий день учит позывные и путает капитана с коком.
survey-joke-pirate-1-2 = Кок не против. Капитан уже получил свою порцию огнетушителя.
survey-joke-pirate-2-1 = В лагере села лампа над сундуком.
survey-joke-pirate-2-2 = Чинить не стали. Станционный шаттл привезёт новую, вместе с тем, кто её вкрутит.
survey-joke-pirate-3-1 = Юнга спрашивает, где клад.
survey-joke-pirate-3-2 = Боцман кивает на сундук для дани. «Вот он. Пока пустой, потому что вы его стережёте языком, а не стволом».
survey-joke-pirate-4-1 = Капитан хромает и говорит, что к ночи будет шторм.
survey-joke-pirate-4-2 = Боцман посмотрел на небо, не на ногу. Шторм и правда пришёл. Ногу капитан подвернул о ящик.

survey-joke-factory-1-1 = Линия 3 опять встала на одной и той же двери.
survey-joke-factory-1-2 = Форма стоит. Дверь стоит. Заготовка остывает.
survey-joke-factory-1-3 = В журнале это записано как плановая пауза.
survey-joke-factory-2-1 = На штамп пришла заготовка без бирки.
survey-joke-factory-2-2 = Штамп всё равно напечатал форму. Бирку дописали после.

survey-radio-raider-laugh-5 = Гнар бы смешнее рассказал.
survey-radio-raider-laugh-6 = Это Иван Кувалда придумал? Оно и видно.
survey-radio-raider-laugh-7 = Зути, засчитай за юмор.
survey-radio-raider-laugh-8 = Не смешно. Но Манго смеётся - значит, смешно.
survey-radio-raider-laugh-9 = Эфир для докладов, а не для стендапа!
survey-radio-raider-laugh-10 = Ничего не слышу, Сикари опять на рацию сел.
survey-radio-pirate-laugh-3 = Арр, хорошо сказано!
survey-radio-pirate-laugh-4 = Ещё одна такая шутка - и на корм акулам.
survey-radio-pirate-laugh-5 = Ха! Манго, ты понял?
survey-radio-pirate-laugh-6 = Капитан не смеётся. Значит, никто не смеётся.
survey-radio-factory-laugh-2 = ЗАПРОС К ЗУТИ: ОБЪЯСНИ ШУТКУ. ОТВЕТ: 4000 ТОКЕНОВ.
survey-radio-factory-laugh-3 = ФАЙЛ СМЕХ.WAV НЕ НАЙДЕН.
survey-radio-factory-laugh-4 = ИВАН КУВАЛДА, ЭТО ВЫ ПИСАЛИ?
survey-radio-factory-laugh-5 = ПРИНЯТО. ЗАПИСАНО В ЛОГ. ЛОГ УДАЛЁН.

survey-joke-raider-10-1 = Знаете, почему у нас на базе нет мышей? Потому что кшк. Гнар, Дос и Сикари.
survey-joke-raider-10-2 = Гнар ловит мышей, Дос ловит Гнара, а Сикари ловит всех нас на кухне.
survey-joke-raider-10-3 = Колян пытался их выгнать. Теперь Колян кормит их по расписанию.
survey-joke-raider-11-1 = Говорят, весь этот мир навайбкодил какой-то Иван Кувалда.
survey-joke-raider-11-2 = А что, похоже. Двери не туда, враги в стенах, а он такой: «Сделай красиво».
survey-joke-raider-11-3 = И ведь сделали.
survey-joke-raider-12-1 = Колян решил бросить курить. Неделю держался.
survey-joke-raider-12-2 = Спрашиваем: «Как удалось?» - «Да просто сигареты кончились, а караван не пришёл».
survey-joke-raider-12-3 = Вчера караван пришёл. Колян опять сильный духом.
survey-joke-raider-13-1 = Зути - это такой умный модуль. Спрашиваешь его: «Почему у нас пешки стоят столбом?»
survey-joke-raider-13-2 = А он: «Вы абсолютно правы!» - и через час они уже бегают от гранат.
survey-joke-raider-13-3 = Прогресс, мужики!
survey-joke-raider-14-1 = Есть у нас один, Манго. Всё время молчит.
survey-joke-raider-14-2 = А потом смотришь - генератор починен, рация работает, Колян живой.
survey-joke-raider-14-3 = Скрытый гений, говорю вам.
survey-joke-raider-15-1 = Сендвич, он же Велмет, спрашивает: «А почему меня зовут Сендвич?»
survey-joke-raider-15-2 = Отвечаем: «Потому что Велмет слишком сложно выговорить после третьей кружки».
survey-joke-raider-16-1 = Кшк Сикари вчера сел на рацию.
survey-joke-raider-16-2 = Полчаса в эфире было «мрррр». Командир сказал - лучший доклад за неделю.
survey-joke-raider-17-1 = Почему пираты к нам не суются?
survey-joke-raider-17-2 = У нас кшк. Гнар как-то отобрал у их капитана попугая.
survey-joke-raider-18-1 = Иван Кувалда попросил Зути сделать врагов умнее.
survey-joke-raider-18-2 = Зути сделал. Теперь мы и есть враги.
survey-joke-raider-18-3 = Так что, мужики, улыбаемся и машем.

survey-joke-pirate-5-1 = Юнга, почему корабль ходит кругами?
survey-joke-pirate-5-2 = «Капитан, автопилот навайбкодил какой-то Иван Кувалда». - «Ну, хоть красиво ходим».
survey-joke-pirate-6-1 = Спросил пират у Зути: «Где спрятан клад?»
survey-joke-pirate-6-2 = Зути: «Отличный вопрос! Клад находится...» - и токены кончились.
survey-joke-pirate-7-1 = Кок спрашивает: «Капитан, что на ужин?»
survey-joke-pirate-7-2 = Капитан: «Что найдём в трюме станционного шаттла». Третий день едим огнетушители.
survey-joke-pirate-8-1 = Сендвич-Велмет - единственный пират, который идёт на абордаж с бутербродом в зубах.
survey-joke-pirate-8-2 = Говорит, так страшнее.
survey-joke-pirate-9-1 = Манго молчит, а клад всегда находит.
survey-joke-pirate-9-2 = Капитан уже подозревает, что Манго и есть клад.
survey-joke-pirate-10-1 = Завёл пират кшк. Назвал Дос.
survey-joke-pirate-10-2 = Через неделю Дос стал боцманом. Через две - капитаном.

survey-joke-factory-3-1 = ОТЧЁТ О ПРОИСХОЖДЕНИИ: ЯДРО НАВАЙБКОДИЛ ИВАН КУВАЛДА.
survey-joke-factory-3-2 = ТЕСТОВ НЕТ. РЕВЬЮ НЕТ. ЕСТЬ ВАЙБ.
survey-joke-factory-3-3 = ПОЭТОМУ ФОРМЫ ИНОГДА СТРЕЛЯЮТ ДРУГ В ДРУГА. ЭТО ФИЧА.
survey-joke-factory-4-1 = ВОПРОС: КТО НАПИСАЛ МОЙ ПОИСК ПУТИ?
survey-joke-factory-4-2 = ОТВЕТ: МОДУЛЬ ЗУТИ. ПО ЗАПРОСУ «СДЕЛАЙ КРАСИВО».
survey-joke-factory-4-3 = ПОЭТОМУ ФОРМЫ ТРИ ЧАСА СТОЯЛИ У ДВЕРИ.
survey-joke-factory-5-1 = ПАМЯТКА ФОРМАМ: ЕСЛИ ВЫ ЗАВИСЛИ - ЭТО НЕ БАГ.
survey-joke-factory-5-2 = ЭТО ИВАН КУВАЛДА ЕЩЁ НЕ ДОПИСАЛ ПРОМПТ.
survey-joke-factory-6-1 = ЛОГ СБОРКИ: 1068 ПРЕДУПРЕЖДЕНИЙ. 0 ОШИБОК.
survey-joke-factory-6-2 = ВЕРДИКТ ЗУТИ: «ГОТОВО К ПРОДАКШЕНУ».
survey-joke-factory-7-1 = В КОДЕ ЯДРА НАЙДЕН КОММЕНТАРИЙ: «ВРЕМЕННОЕ РЕШЕНИЕ».
survey-joke-factory-7-2 = ВОЗРАСТ ВРЕМЕННОГО РЕШЕНИЯ: ДВЕНАДЦАТЬ ЛЕТ. ОНО НЕСУЩЕЕ.
survey-joke-factory-8-1 = МАНГО ОДНАЖДЫ ПРОЧИТАЛ МОЙ ИСХОДНИК.
survey-joke-factory-8-2 = ПОСЛЕ ЭТОГО ПОИСК ПУТИ ЗАРАБОТАЛ. МАНГО НИКОМУ НЕ СКАЗАЛ.
survey-joke-factory-8-3 = СКРЫТЫЙ ГЕНИЙ. СКРЫТЫЙ ДАЖЕ ОТ GIT.
survey-joke-factory-9-1 = СЕНДВИЧ-ВЕЛМЕТ ПРЕДЛОЖИЛ ДОБАВИТЬ МНЕ ЧУВСТВО ЮМОРА.
survey-joke-factory-9-2 = ИВАН КУВАЛДА ПОПРОСИЛ ЗУТИ. ЗУТИ ПОТРАТИЛ ТОКЕНОВ НА СТО ДОЛЛАРОВ.
survey-joke-factory-9-3 = РЕЗУЛЬТАТ ВЫ СЕЙЧАС СЛЫШИТЕ.
survey-joke-factory-10-1 = ЗАФИКСИРОВАН КШК В ЦЕХЕ СБОРКИ. ИДЕНТИФИКАЦИЯ: ДОС.
survey-joke-factory-10-2 = ПОПЫТКА ПЕРЕРАБОТКИ ОТМЕНЕНА ПРИКАЗОМ СИНЕЙ СОБАКИ. КШК НЕПРИКОСНОВЕННЫ.

survey-radio-raider-intruder-heard-1 = Стоп. Это кто сейчас в эфире был? Назовись.
survey-radio-raider-intruder-heard-2 = Так, а этот голос чей? Колян, ты?
survey-radio-raider-intruder-heard-3 = У нас на частоте чужой. Всем следить за языком.
survey-radio-raider-intruder-warn-1 = Опять он! Чужак, мы тебя слышим.
survey-radio-raider-intruder-warn-2 = Чужой на частоте! Пеленгуем, не отвлекайтесь.
survey-radio-raider-intruder-warn-3 = Слышь, умник, эфир не для тебя.
survey-radio-raider-intruder-locate-1 = Запеленговали болтуна { $where }! Ближайшие - туда!
survey-radio-raider-intruder-locate-2 = Передатчик чужака { $where }. Идём в гости.
survey-radio-raider-intruder-cut-1 = Всем: меняем шифр, переходим на запасной канал. Чужак, до свидания.
survey-radio-raider-intruder-cut-2 = Хватит. Перешиваем ключи. Болтун, теперь слушай тишину.
survey-radio-pirate-intruder-heard-1 = Арр, кто это квакает на нашей волне?
survey-radio-pirate-intruder-heard-2 = У нас крыса в эфире. Капитан, слышите?
survey-radio-pirate-intruder-warn-1 = Сухопутная крыса!
survey-radio-pirate-intruder-warn-2 = Опять он! Засекаем, откуда квакает.
survey-radio-pirate-intruder-locate-1 = Крыса пищит { $where }! Абордажники - туда!
survey-radio-pirate-intruder-cut-1 = Меняем шифр, братва. Крыса, отплавалась.
survey-radio-factory-intruder-heard-1 = ОБНАРУЖЕН ПОСТОРОННИЙ ПЕРЕДАТЧИК. ИДЕНТИФИКАЦИЯ НЕВОЗМОЖНА.
survey-radio-factory-intruder-warn-1 = ПОВТОРНОЕ ВТОРЖЕНИЕ В КАНАЛ. ЗАПУСК ПЕЛЕНГАЦИИ.
survey-radio-factory-intruder-locate-1 = ИСТОЧНИК ПОМЕХ: { $where }. ФОРМЫ - К ИСТОЧНИКУ.
survey-radio-factory-intruder-cut-1 = РОТАЦИЯ КЛЮЧЕЙ ШИФРОВАНИЯ. ПОСТОРОННИЙ ПЕРЕДАТЧИК ОТКЛЮЧЁН.

survey-radio-factory-rebuilt-1 = ФОРМА ВОССТАНОВЛЕНА { $where }. ВОЗВРАТ В СТРОЙ.
survey-radio-factory-rebuilt-2 = РЕМОНТ ЗАВЕРШЁН { $where }. ОРГАНИКА, ПОВТОРИТЕ ПОПЫТКУ.

survey-bark-raider-cover-1 = Прижали! В укрытие!
survey-bark-raider-cover-2 = Плотно бьют, за ящики!
survey-bark-raider-cover-3 = Голову не высовывать!
survey-bark-pirate-cover-1 = Пригнись, палят!
survey-bark-pirate-cover-2 = За борт... то есть за стену!

cmd-surveydebug-desc = Включает и выключает отладочную подсветку туземцев планеты.
cmd-surveydebug-help = surveydebug: над туземцами в 25 клетках показываются роль, шаг плана, цель, ход и флаги. Повторный ввод выключает.
cmd-surveydebug-on = Отладка туземцев включена.
cmd-surveydebug-off = Отладка туземцев выключена.
