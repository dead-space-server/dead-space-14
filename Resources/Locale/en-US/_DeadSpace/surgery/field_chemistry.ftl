reagent-name-hemostatin = hemostatin
reagent-desc-hemostatin = Thickens blood inside damaged vessels. Stops internal bleeding without opening the patient. Large doses choke.
reagent-name-calcigen = calcigen
reagent-desc-calcigen = A mineral slurry that fills cracks and knits closed and open fractures. Large doses make the bones ache.
reagent-name-ferrolysin = ferrolysin
reagent-desc-ferrolysin = A sour solvent for lodged shards. Slowly eats metal and glass in wounds and stings a little.
reagent-name-myoregen = myoregen
reagent-desc-myoregen = Wakes up dead tissue. Brings back an arm or leg that withered or died but is still attached. Cannot bring back what was cut off.

reagent-effect-guidebook-limb-mend = { $chance ->
        [1] Heals
       *[other] has a { NATURALPERCENT($chance, 2) } chance to heal
    } limbs from the inside
reagent-effect-guidebook-hemostatin = { $chance ->
        [1] Stops
       *[other] has a { NATURALPERCENT($chance, 2) } chance to stop
    } internal bleeding in limbs
reagent-effect-guidebook-calcigen = { $chance ->
        [1] Knits
       *[other] has a { NATURALPERCENT($chance, 2) } chance to knit
    } closed and open fractures
reagent-effect-guidebook-ferrolysin = { $chance ->
        [1] Dissolves
       *[other] has a { NATURALPERCENT($chance, 2) } chance to dissolve
    } lodged shards
reagent-effect-guidebook-myoregen = { $chance ->
        [1] Brings back
       *[other] has a { NATURALPERCENT($chance, 2) } chance to bring back
    } dead limbs

ent-ChemistryBottleHemostatin = { ent-BaseChemistryBottleFilled }
    .suffix = Hemostatin
    .desc = { ent-BaseChemistryBottleFilled.desc }
ent-ChemistryBottleCalcigen = { ent-BaseChemistryBottleFilled }
    .suffix = Calcigen
    .desc = { ent-BaseChemistryBottleFilled.desc }
ent-ChemistryBottleFerrolysin = { ent-BaseChemistryBottleFilled }
    .suffix = Ferrolysin
    .desc = { ent-BaseChemistryBottleFilled.desc }
ent-ChemistryBottleMyoregen = { ent-BaseChemistryBottleFilled }
    .suffix = Myoregen
    .desc = { ent-BaseChemistryBottleFilled.desc }
