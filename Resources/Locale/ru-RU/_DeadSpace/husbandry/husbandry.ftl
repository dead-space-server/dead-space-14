# Нужды, при осмотре
animal-needs-satiety-satisfied = { CAPITALIZE(SUBJECT($entity)) } выглядит сытым.
animal-needs-satiety-low = { CAPITALIZE(SUBJECT($entity)) } выглядит голодным.
animal-needs-satiety-empty = [color=red]{ CAPITALIZE(SUBJECT($entity)) } умирает от голода![/color]
animal-needs-hydration-satisfied = { CAPITALIZE(SUBJECT($entity)) } не хочет пить.
animal-needs-hydration-low = { CAPITALIZE(SUBJECT($entity)) } хочет пить.
animal-needs-hydration-empty = [color=red]{ CAPITALIZE(SUBJECT($entity)) } умирает от жажды![/color]

# Размножение
husbandry-birth-popup = { CAPITALIZE(THE($parent)) } приносит потомство!
husbandry-laid-egg-popup = { CAPITALIZE(THE($parent)) } откладывает яйцо!

# Сущности
ent-AnimalTrough = поилка для животных
    .desc = Из неё пьют животные. Наполните её из ведра.
ent-AnimalFeeder = кормушка для животных
    .desc = Положите в неё растения, и животные будут есть из неё сами.

# Названия по полу и возрасту
husbandry-name-cow-baby = телёнок
husbandry-name-cow-male = бык
husbandry-name-cow-female = корова
husbandry-name-goat-baby = козлёнок
husbandry-name-goat-male = козёл
husbandry-name-goat-female = коза
husbandry-name-sheep-baby = ягнёнок
husbandry-name-sheep-male = баран
husbandry-name-sheep-female = овца
husbandry-name-pig-baby = поросёнок
husbandry-name-pig-male = хряк
husbandry-name-pig-female = свинья
husbandry-name-chicken-baby = цыплёнок
husbandry-name-chicken-male = петух
husbandry-name-chicken-female = курица
husbandry-name-duck-baby = утёнок
husbandry-name-duck-male = селезень
husbandry-name-duck-female = утка

udder-system-male = У самца нет вымени.
action-popup-lay-egg-male = Самцы не несут яиц.
