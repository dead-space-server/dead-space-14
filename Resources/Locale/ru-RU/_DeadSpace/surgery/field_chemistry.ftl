reagent-name-hemostatin = гемостатин
reagent-desc-hemostatin = Сгущает кровь в повреждённых сосудах изнутри. Останавливает внутреннее кровотечение без вскрытия. В большой дозе душит.
reagent-name-calcigen = кальцеген
reagent-desc-calcigen = Минеральная взвесь, которая заполняет трещины и сращивает закрытые и открытые переломы. В большой дозе ломит кости.
reagent-name-ferrolysin = ферролизин
reagent-desc-ferrolysin = Кислый растворитель для застрявших осколков. Понемногу разъедает металл и стекло в ранах, слегка жжёт.
reagent-name-myoregen = миореген
reagent-desc-myoregen = Будит омертвевшие ткани. Возвращает к работе руку или ногу, которая вытекла или отмерла, но осталась на теле. Отрезанное не вернёт.

reagent-effect-guidebook-limb-mend = { $chance ->
        [1] Лечит
       *[other] с шансом { NATURALPERCENT($chance, 2) } лечит
    } конечности изнутри
reagent-effect-guidebook-hemostatin = { $chance ->
        [1] Останавливает
       *[other] с шансом { NATURALPERCENT($chance, 2) } останавливает
    } внутреннее кровотечение в конечностях
reagent-effect-guidebook-calcigen = { $chance ->
        [1] Сращивает
       *[other] с шансом { NATURALPERCENT($chance, 2) } сращивает
    } переломы, закрытые и открытые
reagent-effect-guidebook-ferrolysin = { $chance ->
        [1] Растворяет
       *[other] с шансом { NATURALPERCENT($chance, 2) } растворяет
    } застрявшие осколки
reagent-effect-guidebook-myoregen = { $chance ->
        [1] Возвращает
       *[other] с шансом { NATURALPERCENT($chance, 2) } возвращает
    } к работе отмершие конечности

ent-ChemistryBottleHemostatin = { ent-BaseChemistryBottleFilled }
    .suffix = Гемостатин
    .desc = { ent-BaseChemistryBottleFilled.desc }
ent-ChemistryBottleCalcigen = { ent-BaseChemistryBottleFilled }
    .suffix = Кальцеген
    .desc = { ent-BaseChemistryBottleFilled.desc }
ent-ChemistryBottleFerrolysin = { ent-BaseChemistryBottleFilled }
    .suffix = Ферролизин
    .desc = { ent-BaseChemistryBottleFilled.desc }
ent-ChemistryBottleMyoregen = { ent-BaseChemistryBottleFilled }
    .suffix = Миореген
    .desc = { ent-BaseChemistryBottleFilled.desc }
