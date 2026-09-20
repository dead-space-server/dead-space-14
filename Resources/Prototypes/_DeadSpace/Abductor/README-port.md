# Abductor Content - Goob C# Port Report

This folder ports the Goob-Station "Abductor" antagonist content into dead-space-14
as a single consolidated file:

- `abductor.yml` - all prototypes (tiles, body, organs, species, mobs, spawners,
  weapons, tools, surgery tools, clothing, structures, actions, objectives,
  antag/roles/mind-roles, gamerules, datasets)
- `abductor.ftl` / `datasets.ftl` - en-US locale (ported verbatim from Goob)
- `Resources/Audio/_DeadSpace/Abductor/abductor.ogg`, `abducted.ogg`
  (copied from Goob `Audio/_Shitmed/Misc/`)
- `Resources/Textures/_DeadSpace/Abductor/abductor_tile.png` (copied from Goob)
- Plus the RSI texture copies already staged under `Textures/_DeadSpace/Abductor/`

The YAML cannot load until the Goob-only C# below is ported. They are grouped by
subsystem with their exact source paths in `Goob-Station`. All path prefixes below
are relative to the Goob-Station repository root.

## Blocking: Abductor core (required first)

These are the actual Abductor gameplay systems:

- `Content.Server/_Shitmed/Antags/Abductor/AbductorSystem.cs`
- `Content.Server/_Shitmed/Antags/Abductor/AbductorSystem.Actions.cs`
- `Content.Server/_Shitmed/Antags/Abductor/AbductorSystem.Console.cs`
- `Content.Server/_Shitmed/Antags/Abductor/AbductorSystem.Gizmo.cs`
- `Content.Server/_Shitmed/Antags/Abductor/AbductorSystem.Vest.cs`
- `Content.Server/_Shitmed/Antags/Abductor/AbductorSystem.Victim.cs`
- `Content.Shared/_Shitmed/Antags/Abductor/SharedAbductorSystem.cs`
- `Content.Shared/_Shitmed/Antags/Abductor/AbductorsComponents.cs`
  (Abductor, AbductorScientist, AbductorVictim, AbductorOrgan, AbductCondition,
   AbductorGizmo, AbductorConsole, AbductorHumanObservationConsole,
   AbductorExperimentator, AbductorVest components)
- `Content.Shared/_Shitmed/Antags/Abductor/AbductorEnums.cs`
- `Content.Shared/_Shitmed/Antags/Abductor/AbductorCameraConsoleUI.cs`
- `Content.Shared/_Shitmed/Antags/Abductor/AbductorReturnDoAfterEvent.cs`
- `Content.Client/_Shitmed/Antags/Abductor/AbductorSystem.cs`
- `Content.Client/_Shitmed/Antags/Abductor/AbductorConsoleBui.cs`
- `Content.Client/_Shitmed/Antags/Abductor/AbductorConsoleWindow.xaml` (+.xaml.cs)
- `Content.Client/_Shitmed/Antags/Abductor/AbductorCameraConsoleBui.cs`
- `Content.Client/_Shitmed/Antags/Abductor/AbductorCameraConsoleWindow.xaml` (+.xaml.cs)

Antag load profile support (`AntagLoadProfileRule` not present in dead-space):
- `Content.Server/Antag/AntagLoadProfileRuleComponent.cs` + system (Goob vanilla fork)
- `Content.Shared/Antag/AntagSelection...` - check dead-space already has this
  (AntagObjectives / AntagSelection / AntagRandomObjectives exist in dead-space).

## Blocking: Shitmed surgery subsystem (reduced)

The abductor surgery TOOLS were removed from `abductor.yml` (all 6 regular tools
and 4 surgery tools deleted, along with their RSI textures; the abductor belt
loadout now uses the stock `Crowbar`/`Wrench`/`Screwdriver`/`Wirecutter`/
`Welder`/`Multitool`, and the surgery duffel is empty). What remains surgery/
Shitmed-facing in the yml:
- `SurgeryTarget` + `Targeting` on `MobAbductor` (and the antag mobs that inherit it)
- `AbductorOrgan` on the dubious glands

So the whole 133-file `Content.Shared/_Shitmed/Surgery/` subsystem (SurgeryTool,
Scalpel, Cautery, etc.) is NO LONGER required by this yml - only the small
`Targeting` (4 files) / `SurgeryTarget` components, or a quick strip of those two
lines from `MobAbductor` if they are not ported first.

## Blocking: objective components (Goob-only)

- `Content.Server/_Shitmed/Objectives/Components/RoleplayObjectiveComponent.cs` (+ system)
- `Content.Server/_Shitmed/Objectives/Components/ForceHereticObjectiveComponent.cs` (+ system)
  (ForceHereticObjective also pulls in Goob's Heretic objective/antag code)
- `AbductCondition` is part of `AbductorsComponents.cs` (above)
- Note: `NumberObjective`, `RoleRequirement`, `NotJobRequirement`,
  `EscapeShuttleCondition`, `AntagRandomObjectives`, `weightedRandom` ALREADY
  exist in dead-space; `objective-issuer-*` keys were added to the locale file.

## Blocking: small components

- `Content.Shared/_Shitmed/ItemSwitch/` (ItemSwitch - 2 files)
- `Content.Shared/_Shitmed/OnHit/` (InjectOnHit, CuffsOnHit, StaminaDamageOnHit helpers - 3 files)
- `Content.Goobstation.Shared/Weapons/Multihit/` (Multihit - 6 files)
- `Content.Shared/_Shitmed/Restrict/` (RestrictInteractionByUserTag, RestrictMeleeByUserTag - 4 files)
- `Content.Shared/_Shitmed/BodyEffects/Subsystems/RandomStatusActivationComponent.cs` + system
- `Content.Shared/_Shitmed/Body/Components/BreathingImmunityComponent.cs`
- `Content.Server/Atmos/Components/PressureImmunityComponent.cs` (+ system)
- `Content.Shared/_Starlight/VentCrawling/` (VentCrawler - 10 files)
- `Content.Shared/_Starlight/CollectiveMind/` (CollectiveMind - 3 files)
- `Content.Goobstation.Shared/GrabIntent/` (GrabIntent, Grabbable - 5 files)
- `Content.Shared/_Shitmed/Targeting/` (Targeting/TargetCursor - 4 files)

## Not needed (already adapted / exists in dead-space)

- Goob body system (new Shitmed BodyPart/body prototypes): the abductor mob now
  inherits the dead-space HUMAN body outright (`MobAbductor` drops its `Body`
  override and uses `Body prototype: Human` from `BaseMobSpecies`). All custom
  `Abductor` body/parts, the `OrganAbductor*` organs, and the abductor damage
  visuals (`WoundableVisuals` brute/burn on abductor parts) were removed so the
  mob runs on human body, human organs, and human damage visuals. The
  `OrganDubious*` experiment glands were kept (they parent `OrganHumanHeart`).
  Consequence: `WoundableVisuals` (Shitmed surgery C#) is no longer required by
  this yml; `AbductorOrgan` is still used by the dubious glands.
- In-hand item textures (`inhand-*` states) were removed from every abductor item
  RSI except `abductor_pistol.rsi`, `gizmo.rsi` and `abductor_wonderprod.rsi`
  (pistol/gizmo/wonderprod keep their hand sprites; everything else renders with
  no texture while held - this is how the client behaves when the RSI lacks the
  `inhand-*` states).
- Dead-space already has: `RandomMetadata`, `BulletDisablerTrace`, `MeatLaserImpact`,
  `Nocturne`, `MuteToxin`, `Cablecuffs`, `AgentIDCard`, `BaseMindRoleAntag`,
  `MindRole`, `roleType`, `XenoArtifact`, `Stealth`, `LightningArcShooter`,
  `GoliathTentacle`, `SolutionRegeneration`.

## Missing assets still to stage

- `Audio/_Goobstation/Music/Abductor.ogg` (shuttle ambience; referenced by the
  Goob shuttle maps) - copy to `Audio/_DeadSpace/Abductor/abductor_music.ogg`
  when the maps are ported.
- Shuttle maps: Goob `Resources/Maps/_Shitmed/...` abductor shuttle maps
  (`/Maps/_DeadSpace/Abductor/abductor_shuttle.yml` and
  `duo_abductor_shuttle.yml` in the gamerules) still need porting.