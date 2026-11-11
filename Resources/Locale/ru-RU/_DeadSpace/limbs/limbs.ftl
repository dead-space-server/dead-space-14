limb-doll-title = Прицел
limb-doll-empty = Часть не выбрана
limb-doll-short-clear = Сброс
limb-analyzer-zone-damage = Урон части:
limb-doll-clear-ring = Сбросить цель
limb-doll-lost = Нет части: { $part }
limb-doll-short-head = Голова
limb-doll-short-eyes = Глаза
limb-doll-short-mouth = Рот
limb-doll-short-torso = Грудь
limb-doll-short-groin = Пах
limb-doll-short-arm-left = Л. рука
limb-doll-short-arm-right = П. рука
limb-doll-short-hand-left = Л. кисть
limb-doll-short-hand-right = П. кисть
limb-doll-short-leg-left = Л. нога
limb-doll-short-leg-right = П. нога
limb-doll-short-foot-left = Л. стопа
limb-doll-short-foot-right = П. стопа

limb-target-eyes = Целиться в глаза
limb-target-mouth = Целиться в рот
limb-target-head = Целиться в голову
limb-target-torso = Целиться в грудь
limb-target-groin = Целиться в пах
limb-harvest-groin = Пах так не отрезать.
limb-target-arm-left = Целиться в левую руку
limb-target-arm-right = Целиться в правую руку
limb-target-hand-left = Целиться в левую кисть
limb-target-hand-right = Целиться в правую кисть
limb-target-leg-left = Целиться в левую ногу
limb-target-leg-right = Целиться в правую ногу
limb-target-foot-left = Целиться в левую стопу
limb-target-foot-right = Целиться в правую стопу
limb-aim-on = Прицельная стрельба
limb-aim-off = Обычная стрельба
limb-fresh = Ещё свежее.
limb-fresh-cold = Холод держит свежесть.
limb-fresh-spent = [color=#7a9a3a]Сгнило.[/color]
limb-grasp-burn = Руки не держат.
limb-harvest-pick = Сначала выберите часть на кукле.
limb-harvest-torso = Грудь так не отрезать.
limb-harvest-gone = Этой части уже нет.
limb-harvest-armored = Сквозь скафандр не прорезать, сначала снимите его.
limb-harvest-start = { CAPITALIZE($user) } { $zone ->
        [Eyes] выкалывает глаза
        [Mouth] вырезает рот
        [Head] отрезает голову
        [ArmLeft] отрезает левую руку
        [ArmRight] отрезает правую руку
        [HandLeft] отрезает левую кисть
        [HandRight] отрезает правую кисть
        [LegLeft] отрезает левую ногу
        [LegRight] отрезает правую ногу
        [FootLeft] отрезает левую стопу
        [FootRight] отрезает правую стопу
       *[other] отрезает часть тела
    } у { $target }!
limb-examine-zone = { $zone }: плоть { $flesh }, кость { $bone }, огонь { $fire }
limb-stand-no-legs = Вы не можете встать
limb-wear-missing = Это не на что надеть.
changecvar-simple-limb_enabled = Включает систему конечностей и ран.
changecvar-full-limb_enabled = Включает систему конечностей и ран: зоны, отрывы, кровотечение частей, куклу прицела. false выключает её для всех.
limb-splint-none = Нет перелома, который нужно зафиксировать.
limb-splint-done = Шина наложена. Кость срастётся, если её не ударить.
limb-peg-none = Нет культи ноги, к которой можно пристегнуть протез.
limb-peg-done = Протез пристёгнут.

ent-FieldSplint = полевая шина
    .desc = Две планки и рулон бинта. Держит сломанную конечность неподвижно, пока кость срастается.
ent-ProstheticPegLeg = деревянный протез
    .desc = Выточенный колышек. Пристегните к культе и ковыляйте домой.
ent-PegLegPartLeft = { ent-ProstheticPegLeg }
    .desc = { ent-ProstheticPegLeg.desc }
ent-PegLegPartRight = { ent-ProstheticPegLeg }
    .desc = { ent-ProstheticPegLeg.desc }

limb-examine-missing-head = Головы нет.
limb-examine-missing-arm-left = Нет левой руки.
limb-examine-missing-arm-right = Нет правой руки.
limb-examine-missing-hand-left = Нет левой кисти.
limb-examine-missing-hand-right = Нет правой кисти.
limb-examine-missing-leg-left = Нет левой ноги.
limb-examine-missing-leg-right = Нет правой ноги.
limb-examine-missing-foot-left = Нет левой стопы.
limb-examine-missing-foot-right = Нет правой стопы.
limb-examine-dead-head = Голова выглядит омертвевшей.
limb-examine-dead-torso = Грудь выглядит омертвевшей.
limb-examine-dead-arm-left = Левая рука выглядит омертвевшей.
limb-examine-dead-arm-right = Правая рука выглядит омертвевшей.
limb-examine-dead-hand-left = Левая кисть выглядит омертвевшей.
limb-examine-dead-hand-right = Правая кисть выглядит омертвевшей.
limb-examine-dead-leg-left = Левая нога выглядит омертвевшей.
limb-examine-dead-leg-right = Правая нога выглядит омертвевшей.
limb-examine-dead-foot-left = Левая стопа выглядит омертвевшей.
limb-examine-dead-foot-right = Правая стопа выглядит омертвевшей.
limb-examine-dead-groin = Пах выглядит омертвевшим.
limb-examine-open-head = Череп разворочен.
limb-examine-open-arm-left = Из левой руки торчит кость.
limb-examine-open-arm-right = Из правой руки торчит кость.
limb-examine-open-hand-left = Из левой кисти торчит кость.
limb-examine-open-hand-right = Из правой кисти торчит кость.
limb-examine-open-leg-left = Из левой ноги торчит кость.
limb-examine-open-leg-right = Из правой ноги торчит кость.
limb-examine-open-foot-left = Из левой стопы торчит кость.
limb-examine-open-foot-right = Из правой стопы торчит кость.
limb-examine-open-groin = Пах разворочен.
limb-examine-broken-arm-left = Левая рука выглядит сломанной.
limb-examine-broken-arm-right = Правая рука выглядит сломанной.
limb-examine-broken-hand-left = Левая кисть выглядит сломанной.
limb-examine-broken-hand-right = Правая кисть выглядит сломанной.
limb-examine-broken-leg-left = Левая нога выглядит сломанной.
limb-examine-broken-leg-right = Правая нога выглядит сломанной.
limb-examine-broken-foot-left = Левая стопа выглядит сломанной.
limb-examine-broken-foot-right = Правая стопа выглядит сломанной.
limb-examine-bleed-head = Из головы идёт кровь.
limb-examine-bleed-torso = Из груди идёт кровь.
limb-examine-bleed-arm-left = Левая рука кровоточит.
limb-examine-bleed-arm-right = Правая рука кровоточит.
limb-examine-bleed-hand-left = Левая кисть кровоточит.
limb-examine-bleed-hand-right = Правая кисть кровоточит.
limb-examine-bleed-leg-left = Левая нога кровоточит.
limb-examine-bleed-leg-right = Правая нога кровоточит.
limb-examine-bleed-foot-left = Левая стопа кровоточит.
limb-examine-bleed-foot-right = Правая стопа кровоточит.
limb-examine-bleed-groin = Из паха идёт кровь.
limb-examine-bleed-heavy-head = Из головы хлещет кровь.
limb-examine-bleed-heavy-torso = Из груди хлещет кровь.
limb-examine-bleed-heavy-arm-left = Из левой руки хлещет кровь.
limb-examine-bleed-heavy-arm-right = Из правой руки хлещет кровь.
limb-examine-bleed-heavy-hand-left = Из левой кисти хлещет кровь.
limb-examine-bleed-heavy-hand-right = Из правой кисти хлещет кровь.
limb-examine-bleed-heavy-leg-left = Из левой ноги хлещет кровь.
limb-examine-bleed-heavy-leg-right = Из правой ноги хлещет кровь.
limb-examine-bleed-heavy-foot-left = Из левой стопы хлещет кровь.
limb-examine-bleed-heavy-foot-right = Из правой стопы хлещет кровь.
limb-examine-bleed-heavy-groin = Из паха хлещет кровь.
limb-examine-limp-left = Хромает на левую ногу.
limb-examine-limp-right = Хромает на правую ногу.
limb-examine-bruise-head = Голова в гематомах.
limb-examine-bruise-torso = Грудь в гематомах.
limb-examine-bruise-arm-left = Левая рука в гематомах.
limb-examine-bruise-arm-right = Правая рука в гематомах.
limb-examine-bruise-hand-left = Левая кисть в гематомах.
limb-examine-bruise-hand-right = Правая кисть в гематомах.
limb-examine-bruise-leg-left = Левая нога в гематомах.
limb-examine-bruise-leg-right = Правая нога в гематомах.
limb-examine-bruise-foot-left = Левая стопа в гематомах.
limb-examine-bruise-foot-right = Правая стопа в гематомах.
limb-examine-bruise-groin = Пах в гематомах.
limb-examine-dent-head = Голова вмята.
limb-examine-dent-torso = Корпус вмят.
limb-examine-dent-arm-left = Левая рука вмята.
limb-examine-dent-arm-right = Правая рука вмята.
limb-examine-dent-hand-left = Левая кисть вмята.
limb-examine-dent-hand-right = Правая кисть вмята.
limb-examine-dent-leg-left = Левая нога вмята.
limb-examine-dent-leg-right = Правая нога вмята.
limb-examine-dent-foot-left = Левая стопа вмята.
limb-examine-dent-foot-right = Правая стопа вмята.
limb-examine-dent-groin = Пах вмят.
limb-examine-rib-open = Из груди торчат рёбра.
limb-examine-whistle = При дыхании из груди свистит.
limb-examine-whistle-machine = Из корпуса свистит воздух.
limb-examine-organ-missing-1 = Глаз нет.
limb-examine-organ-torn-1 = Глаза лопнули.
