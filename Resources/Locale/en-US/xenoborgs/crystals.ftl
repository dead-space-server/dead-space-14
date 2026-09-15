ent-XenoborgExtractorFlatpack = xenoborg extractor flatpack
    .desc = A flatpack used for constructing a xenoborg extractor.

ent-MaterialXenoborgCrystal = xenoborg crystal
    .desc = A special crystal created from nuclear fusion. It is used to make xenoborgs.
    .suffix = 10
ent-MaterialXenoborgCrystal5 = { ent-MaterialXenoborgCrystal }
    .desc = { ent-MaterialXenoborgCrystal.desc }
    .suffix = 5
ent-MaterialXenoborgCrystal1 = { ent-MaterialXenoborgCrystal }
    .desc = { ent-MaterialXenoborgCrystal.desc }
    .suffix = 1

ent-XenoborgExtractor = xenoborg extractor
    .desc = Drains electricity from the grid to produce xenoborg crystals via nuclear fusion.
    .suffix = Unanchored
ent-XenoborgExtractorAnchored = { ent-XenoborgExtractor }
    .desc = { ent-XenoborgExtractor.desc }
    .suffix = Anchored
