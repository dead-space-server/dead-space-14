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
