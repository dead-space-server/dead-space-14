limb-doll-title = Aim
limb-doll-empty = No part selected
limb-doll-short-clear = Clear
limb-analyzer-zone-damage = Part damage:
limb-doll-clear-ring = Clear the target
limb-doll-lost = Missing: { $part }
limb-doll-short-head = Head
limb-doll-short-eyes = Eyes
limb-doll-short-mouth = Mouth
limb-doll-short-torso = Chest
limb-doll-short-groin = Groin
limb-doll-short-arm-left = L. arm
limb-doll-short-arm-right = R. arm
limb-doll-short-hand-left = L. hand
limb-doll-short-hand-right = R. hand
limb-doll-short-leg-left = L. leg
limb-doll-short-leg-right = R. leg
limb-doll-short-foot-left = L. foot
limb-doll-short-foot-right = R. foot
limb-target-clear = Stop aiming a body part
limb-target-eyes = Aim at the eyes
limb-target-mouth = Aim at the mouth
limb-target-head = Aim at the head
limb-target-torso = Aim at the chest
limb-target-groin = Aim at the groin
limb-harvest-groin = The groin does not come off.
limb-target-arm-left = Aim at the left arm
limb-target-arm-right = Aim at the right arm
limb-target-hand-left = Aim at the left hand
limb-target-hand-right = Aim at the right hand
limb-target-leg-left = Aim at the left leg
limb-target-leg-right = Aim at the right leg
limb-target-foot-left = Aim at the left foot
limb-target-foot-right = Aim at the right foot
limb-aim-on = Aimed fire
limb-aim-off = Ordinary fire
limb-fresh = Still fresh.
limb-fresh-cold = The cold is holding it.
limb-fresh-spent = [color=#7a9a3a]It has rotted.[/color]
limb-grasp-burn = The hands cannot hold anything.
limb-harvest-pick = Choose a part on the doll first.
limb-harvest-torso = The chest does not come off this way.
limb-harvest-gone = That part is already gone.
limb-harvest-armored = You can't cut through the suit. Take it off first.
limb-harvest-start = { CAPITALIZE(THE($user)) } is { $zone ->
        [Eyes] gouging out the eyes
        [Mouth] cutting out the mouth
        [Head] cutting off the head
        [ArmLeft] cutting off the left arm
        [ArmRight] cutting off the right arm
        [HandLeft] cutting off the left hand
        [HandRight] cutting off the right hand
        [LegLeft] cutting off the left leg
        [LegRight] cutting off the right leg
        [FootLeft] cutting off the left foot
        [FootRight] cutting off the right foot
       *[other] cutting off a body part
    } of { THE($target) }!
limb-examine-zone = { $zone }: flesh { $flesh }, bone { $bone }, fire { $fire }
limb-stand-no-legs = You can't stand up
limb-wear-missing = There is nothing to put it on.
changecvar-simple-limb_enabled = Turns the limb and wound system on.
changecvar-full-limb_enabled = Turns the limb and wound system on: zones, severing, part bleeding, the aim doll. false turns it off for everyone.
limb-splint-none = There is no fracture to splint.
limb-splint-done = The splint is on. The bone will knit unless it takes another hit.
limb-peg-none = There is no leg stump to strap the peg to.
limb-peg-done = The peg is strapped on.

ent-FieldSplint = field splint
    .desc = Two slats and a roll of bandage. Holds a broken limb in place so it can knit.
ent-ProstheticPegLeg = wooden peg leg
    .desc = A carved peg. Strap it to a stump and hobble home.
ent-PegLegPartLeft = { ent-ProstheticPegLeg }
    .desc = { ent-ProstheticPegLeg.desc }
ent-PegLegPartRight = { ent-ProstheticPegLeg }
    .desc = { ent-ProstheticPegLeg.desc }

limb-examine-missing-head = The head is gone.
limb-examine-missing-arm-left = The left arm is gone.
limb-examine-missing-arm-right = The right arm is gone.
limb-examine-missing-hand-left = The left hand is gone.
limb-examine-missing-hand-right = The right hand is gone.
limb-examine-missing-leg-left = The left leg is gone.
limb-examine-missing-leg-right = The right leg is gone.
limb-examine-missing-foot-left = The left foot is gone.
limb-examine-missing-foot-right = The right foot is gone.
limb-examine-dead-head = The head looks necrotic.
limb-examine-dead-torso = The chest looks necrotic.
limb-examine-dead-arm-left = The left arm looks necrotic.
limb-examine-dead-arm-right = The right arm looks necrotic.
limb-examine-dead-hand-left = The left hand looks necrotic.
limb-examine-dead-hand-right = The right hand looks necrotic.
limb-examine-dead-leg-left = The left leg looks necrotic.
limb-examine-dead-leg-right = The right leg looks necrotic.
limb-examine-dead-foot-left = The left foot looks necrotic.
limb-examine-dead-foot-right = The right foot looks necrotic.
limb-examine-dead-groin = The groin looks necrotic.
limb-examine-open-head = The skull is split open.
limb-examine-open-arm-left = Bone sticks out of the left arm.
limb-examine-open-arm-right = Bone sticks out of the right arm.
limb-examine-open-hand-left = Bone sticks out of the left hand.
limb-examine-open-hand-right = Bone sticks out of the right hand.
limb-examine-open-leg-left = Bone sticks out of the left leg.
limb-examine-open-leg-right = Bone sticks out of the right leg.
limb-examine-open-foot-left = Bone sticks out of the left foot.
limb-examine-open-foot-right = Bone sticks out of the right foot.
limb-examine-open-groin = The groin is split open.
limb-examine-broken-arm-left = The left arm looks broken.
limb-examine-broken-arm-right = The right arm looks broken.
limb-examine-broken-hand-left = The left hand looks broken.
limb-examine-broken-hand-right = The right hand looks broken.
limb-examine-broken-leg-left = The left leg looks broken.
limb-examine-broken-leg-right = The right leg looks broken.
limb-examine-broken-foot-left = The left foot looks broken.
limb-examine-broken-foot-right = The right foot looks broken.
limb-examine-bleed-head = Blood runs from the head.
limb-examine-bleed-torso = Blood runs from the chest.
limb-examine-bleed-arm-left = The left arm is bleeding.
limb-examine-bleed-arm-right = The right arm is bleeding.
limb-examine-bleed-hand-left = The left hand is bleeding.
limb-examine-bleed-hand-right = The right hand is bleeding.
limb-examine-bleed-leg-left = The left leg is bleeding.
limb-examine-bleed-leg-right = The right leg is bleeding.
limb-examine-bleed-foot-left = The left foot is bleeding.
limb-examine-bleed-foot-right = The right foot is bleeding.
limb-examine-bleed-groin = Blood runs from the groin.
limb-examine-bleed-heavy-head = Blood is pouring from the head.
limb-examine-bleed-heavy-torso = Blood is pouring from the chest.
limb-examine-bleed-heavy-arm-left = Blood is pouring from the left arm.
limb-examine-bleed-heavy-arm-right = Blood is pouring from the right arm.
limb-examine-bleed-heavy-hand-left = Blood is pouring from the left hand.
limb-examine-bleed-heavy-hand-right = Blood is pouring from the right hand.
limb-examine-bleed-heavy-leg-left = Blood is pouring from the left leg.
limb-examine-bleed-heavy-leg-right = Blood is pouring from the right leg.
limb-examine-bleed-heavy-foot-left = Blood is pouring from the left foot.
limb-examine-bleed-heavy-foot-right = Blood is pouring from the right foot.
limb-examine-bleed-heavy-groin = Blood is pouring from the groin.
limb-examine-limp-left = Limping on the left leg.
limb-examine-limp-right = Limping on the right leg.
limb-examine-bruise-head = The head is covered in bruises.
limb-examine-bruise-torso = The chest is covered in bruises.
limb-examine-bruise-arm-left = The left arm is covered in bruises.
limb-examine-bruise-arm-right = The right arm is covered in bruises.
limb-examine-bruise-hand-left = The left hand is covered in bruises.
limb-examine-bruise-hand-right = The right hand is covered in bruises.
limb-examine-bruise-leg-left = The left leg is covered in bruises.
limb-examine-bruise-leg-right = The right leg is covered in bruises.
limb-examine-bruise-foot-left = The left foot is covered in bruises.
limb-examine-bruise-foot-right = The right foot is covered in bruises.
limb-examine-bruise-groin = The groin is covered in bruises.
limb-examine-dent-head = The head is dented.
limb-examine-dent-torso = The chassis is dented.
limb-examine-dent-arm-left = The left arm is dented.
limb-examine-dent-arm-right = The right arm is dented.
limb-examine-dent-hand-left = The left hand is dented.
limb-examine-dent-hand-right = The right hand is dented.
limb-examine-dent-leg-left = The left leg is dented.
limb-examine-dent-leg-right = The right leg is dented.
limb-examine-dent-foot-left = The left foot is dented.
limb-examine-dent-foot-right = The right foot is dented.
limb-examine-dent-groin = The groin is dented.
limb-examine-rib-open = Ribs stick out of the chest.
limb-examine-whistle = The chest whistles with each breath.
limb-examine-whistle-machine = Air whistles out of the chassis.
limb-examine-organ-missing-1 = The eyes are gone.
limb-examine-organ-torn-1 = The eyes have burst.
