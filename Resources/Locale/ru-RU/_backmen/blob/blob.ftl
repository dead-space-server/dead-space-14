# Popups
blob-target-normal-blob-invalid = Неподходящий тип блоба, необходимо выбрать нормального блоба.
blob-target-factory-blob-invalid = Неподходящий тип блоба, необходимо выбрать фабрику.
blob-target-node-blob-invalid = Неподходящий тип блоба, необходимо выбрать узел.
blob-target-close-to-resource = Слишком близко к другому ресурсному тайлу.
blob-target-nearby-not-node = Рядом нету узла или ядра.
blob-target-close-to-node = Слишком близко к другому узлу.
blob-mine-invalid-target = Здесь нельзя заложить мину. Мины ставятся на свободные клетки рядом с плитками блоба.
blob-target-already-produce-blobbernaut = Данная фабрика уже произвела блоббернаута.
blob-cant-split = Вы не можете разделить ядро.
blob-not-have-nodes = У вас нету узлов.
blob-not-enough-resources = Не хватает ресурсов для действия.
blob-swap-chem = В разработке.
blob-mob-attack-blob = Вы не можете атаковать блоба.
blob-get-resource = +{ $point }
blob-spent-resource = -{ $point }
blobberaut-not-on-blob-tile = Вы умираете без тайлов блоба под ногами.
blobberaut-factory-destroy = Ваша фабрика была разрушена, вы умираете.
carrier-blob-alert = У вас осталось { $second } секунд до превращения.
blob-mob-zombify-second-start = { $pod } начинает превращать вас в зомби
blob-mob-zombify-third-start = { $pod } начинает превращать { $target } в зомби
blob-mob-zombify-second-end = { $pod } превращает вас в зомби
blob-mob-zombify-third-end = { $pod } превращает { $target } в зомби
# UI
blob-chem-swap-ui-window-name = Смена химиката
blob-chem-reactivespines-info =
    Реактивные шипы
    Наносит 20 единиц брут урона и повышенный структурный урон по строениям.
blob-chem-blazingoil-info =
    Пылающее масло
    Наносит 15 урона ожогами и поджигает цели.
    Делает вас уязвимым к воде.
blob-chem-regenerativemateria-info =
    Регенеративная Материя
    Наносит 15 единиц урона ядами.
    Ядро востанавливает здоровье в 5 раз быстрее и дает на 1 очко больше.
blob-chem-explosivelattice-info =
    Взрывная решетка
    Наносит 5 единиц урона ожогами и взрывает цель, нанося 20 брут урона.
    Споры при смерти взрываются.
    Уничтоженная плитка взрывается на 30 урона на расстоянии 3 клеток.
    Направление взрыва (горизонтально/вертикально) выбирается через ПКМ по плитке.
    Вы получаете имунитет к взрывам и собственному огню.
    Вы получаете на 50% больше урона ожогами и электричеством.
blob-chem-electromagneticweb-info =
    Электромагнитная паутина
    Наносит 20 урона ожогами, 20% шанс вызывать ЭМИ разряд при атаке.
    Любая уничтоженая плитка гарантировано вызовет ЭМИ в радиусе 5 клеток:
    разряжает оружие, свет и технику, нанося 20 урона ожогами.
    Вы получаете на 25% больше урона теплом и брутом.
blob-alert-out-off-station = Блоб был удален т.к. был обнаружен вне станции!

# game-rule

blob-title = Блоб
blob-description = Показатели биологической безопасности станции показывают наличие на станции некого паразита...
# Announcment
blob-alert-recall-shuttle = Эвакуационный шатл не может быть отправлен на станцию пока существует биологическая угроза 5 уровня.
blob-alert-detect = На станции была обнаружена биологическая угроза 5 уровня, обьявлена изоляция станции.
blob-alert-critical =
    Внимание, командование и экипаж станции объекта { $station }, на связи Корпус Специальных Операций.

    Локальными системами станции зафиксирован высокий уровень биологического заражения в связи с критической массой биологической угрозы пятого уровня. Согласно утверждённому приказу на факс Капитана объекта были высланы коды ядерной аутентификации.

    Согласно распоряжению Офицера Специальных Операций должны быть инициированы протоколы самоуничтожения станции с дальнейшей эвакуацией экипажа любыми доступными средствами. Боже, Директор Храни!
# Ghost role
blob-carrier-role-name = Носитель блоба
blob-carrier-role-desc = Сущность зараженная "блобом".
blob-carrier-role-rules =
    Вы антагонист. У вас есть 4 минуты перед тем как вы превратитесь в блоба.
    Найдите за это время укромное место для стартовой точки заражения станции, ведь вы очень слабы в первые минуты после создания ядра.
blob-carrier-role-greeting = Вы носитель Блоба. Найдите укромное место на станции и превратитесь в Блоба. Превратите станцию в массу, а ее обитателей в ваших слуг. Все мы Блоб.
# Verbs
blob-pod-verb-zombify = Зомбировать
blob-verb-upgrade-to-strong = Улучшить до сильного блоба
blob-verb-upgrade-to-reflective = Улучшить до отражающего блоба
blob-verb-remove-blob-tile = Убрать блоба
blob-verb-explosion-direction-toggle = Направление взрыва: { $dir }
blob-explosion-direction-horizontal = горизонтально
blob-explosion-direction-vertical = вертикально
blob-verb-explosion-direction = Взрыв теперь бахает { $dir }
# Alerts
blob-resource-alert-name = Ресурсы ядра
blob-resource-alert-desc = Ваши ресурсы которые производят ресурсные блобы и само ядро, требуются для разрастанция и особых блобов.
blob-health-alert-name = Здоровье ядра
blob-health-alert-desc = Здоровье вашего ядра. Если оно опустится до 0 вы умрёте.
# Greeting
blob-role-greeting =
    Вы блоб - космический паразит который захватывает станции.
    Ваша цель - стать как можно больше не дав себя уничтожить.
    Используйте горячие клавиши Alt+LMB чтобы улучшать обычные плитки до сильных а сильные до отражающих.
    Позаботьтесь о получении ресурсов с блобов ресурсов.
    Вы практически неуязвимы к физическим повреждениям, но опасайтесь теплового урона.
    Учтите что особые клетки блоба работают только возле узлов или ядра.
blob-zombie-greeting = Вы были заражены спорой блоба которая вас воскресила, теперь вы действуете в интересах блоба.
# End round
blob-round-end-result =
    { $blobCount ->
        [one] Был один блоб.
       *[other] Было { $blobCount } блобов.
    }
blob-user-was-a-blob = [color=gray]{ $user }[/color] был блобом.
blob-user-was-a-blob-named = [color=White]{ $name }[/color] ([color=gray]{ $user }[/color]) был блобом.
blob-was-a-blob-named = [color=White]{ $name }[/color] был блобом.
objective-issuer-blob = [color=#33cc00]Блоб[/color]
blob-user-was-a-blob-with-objectives = [color=gray]{ $user }[/color] был блобом и имел следующие цели:
blob-user-was-a-blob-with-objectives-named = [color=White]{ $name }[/color] ([color=gray]{ $user }[/color]) был блобом и имел следующие цели:
blob-was-a-blob-with-objectives-named = [color=White]{ $name }[/color] был блобом и имел следующие цели:
# Objectivies
objective-condition-blob-capture-title = Захватить станцию
objective-condition-blob-capture-description = Ваша единственная цель - полное и безоговорочное поглощение станции. Вам необходимо владеть как минимум { $count } тайлами блоба.
roles-antag-blob-name = Блоб
roles-antag-blob-objective = Захватите станцию, поглотив её целиком.
objective-condition-success = { $condition } | [color={ $markupColor }]Успех![/color]
objective-condition-fail = { $condition } | [color={ $markupColor }]Провал![/color] ({ $progress }%)
blob-mine-tile-name = мина блоба
blob-mine-tile-desc = Спрятанная в биомассе мина. От неё лучше держаться подальше.
blob-plant-mine-action-name = Заложить мину (25)
blob-plant-mine-action-desc = Превращает выбранную клетку рядом с плитками блоба в мину. Мина невидима, а наступивший на неё поджигается и получает 15-20 урона термами.
