#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.Client.DeadSpace.Psychiatry;
using Content.Shared.Traits.Assorted;
using Robust.Server.Player;
using Robust.Shared.Maths;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Cargo.Systems;
using Content.Server.Chat.Systems;
using Content.Server.DeadSpace.Psychiatry;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Changeling.Components;
using Content.Shared.Sirena.Animations;
using Content.Shared.Changeling.Systems;
using Content.Shared.Construction.Prototypes;
using Content.Shared.DeadSpace.Construction;
using Content.Shared.EntityEffects.Effects.Damage;
using Content.Shared.Body.Components;
using Content.Shared.Body.Prototypes;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.EntityEffects;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Damage;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.DeadSpace.Psychiatry;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost;
using Content.Shared.MedicalScanner;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.VendingMachines;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.DeadSpace.Psychiatry;

[TestOf(typeof(SchizophreniaComponent))]
public sealed class PsychiatryTest : InteractionTest
{
    private static readonly ProtoId<PsychiatryRemapPrototype> MeatWallRemap = "PsychiatryRemapMeatWall";
    private static readonly ProtoId<ReagentPrototype> Synaptizine = "Synaptizine";
    private static readonly ProtoId<ReactionPrototype> NeuroClarityReaction = "NeuroClarity";
    private static readonly ProtoId<VendingMachineInventoryPrototype> NanoMedInventory = "NanoMedInventory";
    private static readonly ProtoId<VendingMachineInventoryPrototype> NanoMedPlusInventory = "NanoMedPlusInventory";
    private static readonly ProtoId<CargoProductPrototype> MedicalRestock = "CrateVendingMachineRestockMedical";
    private static readonly ProtoId<EmotePrototype> Polskorovit = "Polskorovit";
    private static readonly ProtoId<ReagentPrototype> NeuroClarity = "NeuroClarity";
    private static readonly ProtoId<MetabolismGroupPrototype> MedicineGroup = "Medicine";
    private static readonly ProtoId<ConstructionPrototype> FirmwarePatchCraft = "FirmwarePatch";
    private static readonly ProtoId<ConstructionPrototype> CascadeSpikeCraft = "CascadeSpike";

    protected override string PlayerPrototype => "MobHuman";

    [Test]
    public async Task SchizotoxinAppliesAcute()
    {
        await Server.WaitAssertion(() =>
        {
            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            Assert.That(SEntMan.TryGetComponent<BloodstreamComponent>(SPlayer, out var blood), Is.True);
            Assert.That(solutions.TryGetSolution(SPlayer, blood!.BloodSolutionName, out var soln, out _), Is.True);
            Assert.That(solutions.TryAddReagent(soln!.Value, "Schizotoxin", FixedPoint2.New(10)), Is.True);
        });

        await RunSeconds(1.5f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<SchizophreniaComponent>(SPlayer), Is.True);
            var schizo = SEntMan.GetComponent<SchizophreniaComponent>(SPlayer);
            Assert.That(schizo.Stage, Is.EqualTo(SchizophreniaStage.Acute));
            Assert.That(schizo.PillForced, Is.True);
            Assert.That(schizo.Seed, Is.Not.EqualTo(0));
        });
    }

    [Test]
    public async Task NeuroClarityCourseMatchesStage()
    {
        await Server.WaitPost(() =>
        {
            var psych = SEntMan.System<PsychiatrySystem>();
            psych.ApplyNew(SPlayer, SchizophreniaStage.Latent, pillForced: false, reason: "test");
            psych.ApplyClarityDose(SPlayer, 15f);
            Assert.That(SEntMan.HasComponent<SchizophreniaComponent>(SPlayer), Is.False);

            psych.ApplyNew(SPlayer, SchizophreniaStage.Simple, pillForced: false, reason: "test");
            psych.ApplyClarityDose(SPlayer, 15f);
            Assert.That(SEntMan.GetComponent<SchizophreniaComponent>(SPlayer).Stage, Is.EqualTo(SchizophreniaStage.Simple));
            psych.ApplyClarityDose(SPlayer, 15f);
            Assert.That(SEntMan.HasComponent<SchizophreniaComponent>(SPlayer), Is.False);

            psych.ApplyNew(SPlayer, SchizophreniaStage.Acute, pillForced: false, reason: "test");
            psych.ApplyClarityDose(SPlayer, 15f);
            psych.ApplyClarityDose(SPlayer, 15f);
            Assert.That(SEntMan.HasComponent<SchizophreniaComponent>(SPlayer), Is.True);
            psych.ApplyClarityDose(SPlayer, 15f);
            Assert.That(SEntMan.HasComponent<SchizophreniaComponent>(SPlayer), Is.False);
        });
    }

    [Test]
    public async Task NeuroClarityMetabolismTickIsNotAWholePill()
    {
        await Server.WaitPost(() =>
        {
            var psych = SEntMan.System<PsychiatrySystem>();
            psych.ApplyNew(SPlayer, SchizophreniaStage.Latent, pillForced: false, reason: "test");
            SEntMan.System<SharedEntityEffectsSystem>().ApplyEffect(SPlayer, new PsychiatryClarityDose(), 1f);
            var schizo = SEntMan.GetComponent<SchizophreniaComponent>(SPlayer);
            Assert.That(schizo.Stage, Is.EqualTo(SchizophreniaStage.Latent));
            Assert.That(schizo.CourseTaken, Is.EqualTo(0));
            Assert.That(schizo.CourseMetabolized, Is.EqualTo(0.05f).Within(0.001f));
        });
    }

    [Test]
    public async Task ItemHoverEmotesSkipPolskorovit()
    {
        await Server.WaitAssertion(() =>
        {
            var ids = new List<string>();
            PsychiatryClientSystem.CollectItemHoverEmotes(ProtoMan, ids);
            Assert.That(ids, Does.Not.Contain("Polskorovit"));
            Assert.That(ids, Does.Contain("EmoteJump"));
            Assert.That(ids, Does.Contain("EmoteFlip"));
            Assert.That(ids, Does.Contain("EmoteBow"));
            Assert.That(ids, Does.Contain("EmoteTurn"));
            Assert.That(ids, Does.Contain("EmoteBreakdance"));
        });
    }

    [Test]
    public async Task OnsetCooldownBlocksRapidTriggers()
    {
        await Server.WaitPost(() =>
        {
            var psych = SEntMan.System<PsychiatrySystem>();
            Assert.That(psych.TryOnsetOrEscalate(SPlayer, SchizophreniaStage.Latent, "test-a"), Is.True);
            Assert.That(psych.TryOnsetOrEscalate(SPlayer, SchizophreniaStage.Latent, "test-b"), Is.False);
            Assert.That(SEntMan.GetComponent<SchizophreniaComponent>(SPlayer).Stage, Is.EqualTo(SchizophreniaStage.Latent));
        });
    }

    [Test]
    public async Task PillIgnoresCooldownAndSetsPillForced()
    {
        await Server.WaitPost(() =>
        {
            var psych = SEntMan.System<PsychiatrySystem>();
            Assert.That(psych.TryOnsetOrEscalate(SPlayer, SchizophreniaStage.Latent, "test-a"), Is.True);
            Assert.That(psych.TryApplyOrEscalate(SPlayer, SchizophreniaStage.Acute, pillForced: true, ignoreCooldown: true, reason: "pill"), Is.True);
            var schizo = SEntMan.GetComponent<SchizophreniaComponent>(SPlayer);
            Assert.That(schizo.Stage, Is.EqualTo(SchizophreniaStage.Simple));
            Assert.That(schizo.PillForced, Is.True);
        });
    }

    [Test]
    public async Task AutoEscalateAdvancesStage()
    {
        await Server.WaitPost(() =>
        {
            var psych = SEntMan.System<PsychiatrySystem>();
            var timing = Server.ResolveDependency<IGameTiming>();
            psych.ApplyNew(SPlayer, SchizophreniaStage.Latent, pillForced: false, reason: "test");
            var schizo = SEntMan.GetComponent<SchizophreniaComponent>(SPlayer);
            schizo.NextAutoEscalate = timing.CurTime - TimeSpan.FromSeconds(1);
            SEntMan.Dirty(SPlayer, schizo);
        });

        await RunSeconds(1.5f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<SchizophreniaComponent>(SPlayer).Stage, Is.EqualTo(SchizophreniaStage.Simple));
        });
    }

    [Test]
    public async Task TreatmentClearsAtZero()
    {
        await Server.WaitPost(() =>
        {
            var psych = SEntMan.System<PsychiatrySystem>();
            psych.ApplyNew(SPlayer, SchizophreniaStage.Latent, pillForced: false, reason: "test");
            psych.AdjustStage(SPlayer, -1, "clear");
            Assert.That(SEntMan.GetComponent<SchizophreniaComponent>(SPlayer).Stage, Is.EqualTo(SchizophreniaStage.Incipient));
            psych.AdjustStage(SPlayer, -1, "clear");
            Assert.That(SEntMan.HasComponent<SchizophreniaComponent>(SPlayer), Is.False);
        });
    }

    [Test]
    public async Task LatentMinusTwoClears()
    {
        await Server.WaitPost(() =>
        {
            var psych = SEntMan.System<PsychiatrySystem>();
            psych.ApplyNew(SPlayer, SchizophreniaStage.Latent, pillForced: false, reason: "test");
            psych.AdjustStage(SPlayer, -2, "drop");
            Assert.That(SharedPsychiatrySystem.LowerStage(SchizophreniaStage.Latent, 2), Is.EqualTo(SchizophreniaStage.None));
            Assert.That(SEntMan.HasComponent<SchizophreniaComponent>(SPlayer), Is.False);
        });
    }

    [Test]
    public async Task HealthAnalyzerStateHasNoSchizophreniaField()
    {
        await Server.WaitAssertion(() =>
        {
            var state = new HealthAnalyzerUiState();
            Assert.That(typeof(HealthAnalyzerUiState).GetField("Schizophrenia"), Is.Null);
            Assert.That(typeof(HealthAnalyzerUiState).GetProperty("Schizophrenia"), Is.Null);
            Assert.That(typeof(HealthAnalyzerUiState).GetField("MentalIllness"), Is.Null);
            _ = state;
        });
    }

    [Test]
    public void StageClampHelpers()
    {
        Assert.That(SharedPsychiatrySystem.ClampStage(0), Is.EqualTo(SchizophreniaStage.None));
        Assert.That(SharedPsychiatrySystem.ClampStage(99), Is.EqualTo(SchizophreniaStage.Acute));
        Assert.That(SharedPsychiatrySystem.LowerStage(SchizophreniaStage.Acute, 2), Is.EqualTo(SchizophreniaStage.Latent));
        Assert.That(SharedPsychiatrySystem.LowerStage(SchizophreniaStage.Latent, 2), Is.EqualTo(SchizophreniaStage.None));
    }

    [Test]
    public async Task PatternPoolsByStage()
    {
        Assert.That(PsychiatryPattern.PickPool(SchizophreniaStage.Latent, 1, 1), Is.EqualTo(PsychiatryRemapPool.Animal));
        Assert.That(PsychiatryPattern.PickPool(SchizophreniaStage.Simple, 1, 1), Is.EqualTo(PsychiatryRemapPool.Animal));
        Assert.That(PsychiatryPattern.ShouldRemapMob(42, 7, SchizophreniaStage.None), Is.False);
        Assert.That(PsychiatryPattern.ShouldRemapMob(1, 1, SchizophreniaStage.Incipient, 1f, 1f, 1f), Is.False);
        Assert.That(PsychiatryPattern.ShouldRemapMob(1, 1, SchizophreniaStage.Latent, 1f, 1f, 1f), Is.False);
        Assert.That(PsychiatryPattern.ShouldRemapMob(1, 1, SchizophreniaStage.Simple, 0f, 1f, 1f), Is.True);
        Assert.That(PsychiatryPattern.ShouldRemapItem(1, 1, SchizophreniaStage.Simple, 1f, 1f, 1f), Is.False);
        Assert.That(PsychiatryPattern.IsMeatWall(new Vector2i(3, 9), 123, SchizophreniaStage.Simple), Is.False);

        await Server.WaitAssertion(() =>
        {
            var protos = Server.ResolveDependency<IPrototypeManager>();
            Assert.That(protos.TryIndex(MeatWallRemap, out var wall), Is.True);
            Assert.That(wall!.Sprite, Is.InstanceOf<SpriteSpecifier.Rsi>());
            Assert.That(((SpriteSpecifier.Rsi) wall.Sprite).RsiState, Is.EqualTo("full"));
        });
    }

    [Test]
    public async Task ChemForceLatent()
    {
        await Server.WaitPost(() =>
        {
            var psych = SEntMan.System<PsychiatrySystem>();
            psych.ApplyPsychogenDose(SPlayer, 4f);
            Assert.That(SEntMan.HasComponent<SchizophreniaComponent>(SPlayer), Is.False);

            psych.ApplyPsychogenDose(SPlayer, 1f);
            Assert.That(SEntMan.GetComponent<SchizophreniaComponent>(SPlayer).Stage, Is.EqualTo(SchizophreniaStage.Latent));

            psych.ApplyPsychogenDose(SPlayer, 10f);
            Assert.That(SEntMan.GetComponent<SchizophreniaComponent>(SPlayer).Stage, Is.EqualTo(SchizophreniaStage.Acute));

            psych.ApplyPsychogenDose(SPlayer, 10f);
            Assert.That(SEntMan.GetComponent<SchizophreniaComponent>(SPlayer).Stage, Is.EqualTo(SchizophreniaStage.Acute));
        });
    }

    [Test]
    public async Task PillForcedBypassesAntagImmuneHelper()
    {
        await Server.WaitAssertion(() =>
        {
            var psych = SEntMan.System<PsychiatrySystem>();
            Assert.That(psych.IsAntagImmune(SPlayer, pillForced: true), Is.False);
            Assert.That(psych.TryApplyOrEscalate(SPlayer, SchizophreniaStage.Acute, pillForced: true, ignoreCooldown: true, reason: "pill"), Is.True);
            Assert.That(SEntMan.GetComponent<SchizophreniaComponent>(SPlayer).PillForced, Is.True);
        });
    }

    [Test]
    public async Task AsphyxiationRollCanOnset()
    {
        await Server.WaitAssertion(() =>
        {
            Server.CfgMan.SetCVar(CCCCVars.PsychiatryAsphyxiationChance, 0f);
            var damage = new DamageSpecifier();
            damage.DamageDict["Asphyxiation"] = FixedPoint2.New(80);
            Assert.That(SEntMan.System<DamageableSystem>().TryChangeDamage(SPlayer, damage), Is.True);
            Server.CfgMan.SetCVar(CCCCVars.PsychiatryAsphyxiationChance, 0.005f);
            Assert.That(SEntMan.System<PsychiatryOnsetSystem>().TryAsphyxiationRoll(SPlayer, 0f), Is.True);
            Assert.That(SEntMan.GetComponent<SchizophreniaComponent>(SPlayer).Stage, Is.EqualTo(SchizophreniaStage.Simple));
        });
    }

    [Test]
    public async Task SlipRollCanOnset()
    {
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<PsychiatryOnsetSystem>().TrySlipRoll(SPlayer, 0f), Is.True);
            Assert.That(SEntMan.GetComponent<SchizophreniaComponent>(SPlayer).Stage, Is.EqualTo(SchizophreniaStage.Latent));
        });
    }

    [Test]
    public async Task PositronicGetsCyberpsychosisNotSchizophrenia()
    {
        EntityUid ipc = default;
        await Server.WaitPost(() =>
        {
            var coords = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            ipc = SEntMan.SpawnEntity("MobIPC", coords);
        });

        await Server.WaitAssertion(() =>
        {
            var psych = SEntMan.System<PsychiatrySystem>();
            Assert.That(psych.IsPositronic(ipc), Is.True);
            Assert.That(psych.TryOnsetOrEscalate(ipc, SchizophreniaStage.Acute, "organic"), Is.False);
            Assert.That(SEntMan.HasComponent<SchizophreniaComponent>(ipc), Is.False);
            Assert.That(psych.TryApplyCyber(ipc, SchizophreniaStage.Simple, "emp", ignoreCooldown: true), Is.True);
            var illness = SEntMan.GetComponent<SchizophreniaComponent>(ipc);
            Assert.That(illness.Kind, Is.EqualTo(PsychiatryIllnessKind.Cyberpsychosis));
            Assert.That(illness.Stage, Is.EqualTo(SchizophreniaStage.Simple));
            psych.AdjustStage(ipc, -(int) SchizophreniaStage.Simple, "hard-reset");
            Assert.That(SEntMan.HasComponent<SchizophreniaComponent>(ipc), Is.False);
            Assert.That(SEntMan.System<PsychiatryOnsetSystem>().TrySlipRoll(ipc, 0f), Is.True);
            var slipped = SEntMan.GetComponent<SchizophreniaComponent>(ipc);
            Assert.That(slipped.Kind, Is.EqualTo(PsychiatryIllnessKind.Cyberpsychosis));
            Assert.That(slipped.Stage, Is.EqualTo(SchizophreniaStage.Latent));
        });
    }

    [Test]
    public async Task OnsetScanSkipsNonPlayersAndUnplayableBodies()
    {
        await Server.WaitAssertion(() =>
        {
            var onset = SEntMan.System<PsychiatryOnsetSystem>();
            var coords = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            var mouse = SEntMan.SpawnEntity("MobMouse", coords);
            var bystander = SEntMan.SpawnEntity("MobHuman", coords);
            SEntMan.EnsureComponent<ActorComponent>(mouse);

            Assert.That(onset.IsOnsetCandidate(SPlayer), Is.True);
            Assert.That(onset.IsOnsetCandidate(mouse), Is.False);
            Assert.That(onset.IsOnsetCandidate(bystander), Is.False);
        });
    }

    [Test]
    public async Task TickingDamageRollsOncePerGap()
    {
        await Server.WaitAssertion(() =>
        {
            Server.CfgMan.SetCVar(CCCCVars.PsychiatryAsphyxiationChance, 0f);
            Server.CfgMan.SetCVar(CCCCVars.PsychiatryRadiationChance, 0f);
            Server.CfgMan.SetCVar(CCCCVars.PsychiatryDamageRollGapSec, 2f);
            var damage = new DamageSpecifier();
            damage.DamageDict["Asphyxiation"] = FixedPoint2.New(80);
            damage.DamageDict["Radiation"] = FixedPoint2.New(80);
            Assert.That(SEntMan.System<DamageableSystem>().TryChangeDamage(SPlayer, damage), Is.True);
            Assert.That(SEntMan.HasComponent<SchizophreniaComponent>(SPlayer), Is.False);
            var tracker = SEntMan.GetComponent<SchizophreniaOnsetTrackerComponent>(SPlayer);
            var now = Server.ResolveDependency<IGameTiming>().CurTime;
            Assert.That(tracker.NextAsphyxiationRoll, Is.GreaterThan(now));
            Assert.That(tracker.NextRadiationRoll, Is.GreaterThan(now));

            Server.CfgMan.SetCVar(CCCCVars.PsychiatryAsphyxiationChance, 1f);
            Server.CfgMan.SetCVar(CCCCVars.PsychiatryRadiationChance, 1f);
            var more = new DamageSpecifier();
            more.DamageDict["Asphyxiation"] = FixedPoint2.New(1);
            more.DamageDict["Radiation"] = FixedPoint2.New(1);
            Assert.That(SEntMan.System<DamageableSystem>().TryChangeDamage(SPlayer, more), Is.True);
            Assert.That(SEntMan.HasComponent<SchizophreniaComponent>(SPlayer), Is.False);
        });

        await RunSeconds(2.1f);

        await Server.WaitAssertion(() =>
        {
            var more = new DamageSpecifier();
            more.DamageDict["Asphyxiation"] = FixedPoint2.New(1);
            Assert.That(SEntMan.System<DamageableSystem>().TryChangeDamage(SPlayer, more), Is.True);
            Assert.That(SEntMan.GetComponent<SchizophreniaComponent>(SPlayer).Stage, Is.EqualTo(SchizophreniaStage.Simple));
            Server.CfgMan.SetCVar(CCCCVars.PsychiatryAsphyxiationChance, 0.005f);
            Server.CfgMan.SetCVar(CCCCVars.PsychiatryRadiationChance, 0.02f);
            Server.CfgMan.SetCVar(CCCCVars.PsychiatryDamageRollGapSec, 2f);

            var coords = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            var mouse = SEntMan.SpawnEntity("MobMouse", coords);
            var bitten = new DamageSpecifier();
            bitten.DamageDict["Asphyxiation"] = FixedPoint2.New(80);
            Assert.That(SEntMan.System<DamageableSystem>().TryChangeDamage(mouse, bitten), Is.True);
            Assert.That(SEntMan.HasComponent<SchizophreniaOnsetTrackerComponent>(mouse), Is.False);
        });
    }

    [Test]
    public async Task SynaptizineClearsBlindDeafAndMute()
    {
        await Server.WaitAssertion(() =>
        {
            Assert.That(ProtoMan.TryIndex(Synaptizine, out var reagent), Is.True);
            ClearSensoryFault? fault = null;
            if (reagent!.Metabolisms != null)
            {
                foreach (var entry in reagent.Metabolisms.Values)
                {
                    foreach (var effect in entry.Effects)
                    {
                        if (effect is ClearSensoryFault sensory)
                            fault = sensory;
                    }
                }
            }

            Assert.That(fault, Is.Not.Null);
            Assert.That(fault!.Probability, Is.EqualTo(1f));

            var status = SEntMan.System<StatusEffectsSystem>();
            Assert.That(status.TrySetStatusEffectDuration(SPlayer, "StatusEffectDeaf", null), Is.True);
            Assert.That(status.TrySetStatusEffectDuration(SPlayer, "StatusEffectMuted", null), Is.True);
            Assert.That(status.HasStatusEffect(SPlayer, "StatusEffectDeaf"), Is.True);
            Assert.That(status.HasStatusEffect(SPlayer, "StatusEffectMuted"), Is.True);

            Assert.That(SEntMan.TryGetComponent<BlindableComponent>(SPlayer, out var eyes), Is.True);
            var blind = SEntMan.System<BlindableSystem>();
            blind.AdjustEyeDamage((SPlayer, eyes), eyes!.MaxDamage - eyes.EyeDamage);
            Assert.That(eyes.EyeDamage, Is.EqualTo(eyes.MaxDamage));

            SEntMan.System<SharedEntityEffectsSystem>().ApplyEffect(SPlayer, fault!);
        });

        await RunTicks(1);

        await Server.WaitAssertion(() =>
        {
            var status = SEntMan.System<StatusEffectsSystem>();
            Assert.That(status.HasStatusEffect(SPlayer, "StatusEffectDeaf"), Is.False);
            Assert.That(status.HasStatusEffect(SPlayer, "StatusEffectMuted"), Is.False);
            Assert.That(SEntMan.GetComponent<BlindableComponent>(SPlayer).EyeDamage, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task NeuroClarityRecipeTakesOneCarbon()
    {
        await Server.WaitAssertion(() =>
        {
            Assert.That(ProtoMan.TryIndex(NeuroClarityReaction, out var reaction), Is.True);
            Assert.That(reaction!.Reactants.Count, Is.EqualTo(5));
            Assert.That(reaction.Reactants["Carbon"].Amount, Is.EqualTo(FixedPoint2.New(1)));
            Assert.That(reaction.Reactants["Benzene"].Amount, Is.EqualTo(FixedPoint2.New(1)));
            Assert.That(reaction.Reactants["Dylovene"].Amount, Is.EqualTo(FixedPoint2.New(1)));
            Assert.That(reaction.Reactants["Synaptizine"].Amount, Is.EqualTo(FixedPoint2.New(1)));
            Assert.That(reaction.Reactants["Mannitol"].Amount, Is.EqualTo(FixedPoint2.New(1)));
            Assert.That(reaction.Products["NeuroClarity"], Is.EqualTo(FixedPoint2.New(2)));
        });
    }

    [TestCase("MindRoleRevolutionary")]
    [TestCase("MindRoleChangeling")]
    public async Task BecomingAntagClearsSchizophreniaAndKeepsInjectionAndGas(string roleId)
    {
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.CfgMan.GetCVar(CCCCVars.PsychiatryAntagImmunityMode), Is.EqualTo((int) PsychiatryAntagImmunity.Partial));
            var minds = SEntMan.System<SharedMindSystem>();
            var roles = SEntMan.System<SharedRoleSystem>();
            var psych = SEntMan.System<PsychiatrySystem>();
            var mind = minds.GetOrCreateMind(ServerSession.UserId);
            minds.TransferTo(mind.Owner, SPlayer, mind: mind.Comp);
            psych.ApplyNew(SPlayer, SchizophreniaStage.Simple, pillForced: false, reason: "test");
            roles.MindAddRole(mind.Owner, roleId, mind.Comp, silent: true);

            Assert.That(SEntMan.HasComponent<SchizophreniaComponent>(SPlayer), Is.False);
            Assert.That(psych.IsAntagImmune(SPlayer, pillForced: false), Is.True);
            Assert.That(psych.IsAntagImmune(SPlayer, pillForced: true), Is.False);
            Assert.That(psych.IsAntagImmune(SPlayer, pillForced: false, gas: true), Is.False);
            Assert.That(psych.TryOnsetOrEscalate(SPlayer, SchizophreniaStage.Latent, "slip"), Is.False);
            Assert.That(psych.TryInhalePsychogen(SPlayer), Is.True);
            Assert.That(SEntMan.GetComponent<SchizophreniaComponent>(SPlayer).Stage, Is.EqualTo(SchizophreniaStage.Latent));
            Assert.That(psych.TryApplyOrEscalate(SPlayer, SchizophreniaStage.Acute, pillForced: true, ignoreCooldown: true, reason: "pill"), Is.True);
            Assert.That(SEntMan.GetComponent<SchizophreniaComponent>(SPlayer).Stage, Is.EqualTo(SchizophreniaStage.Simple));
        });
    }

    [Test]
    public async Task PositronicAntagClearsCyberAndKeepsCascadeSpike()
    {
        EntityUid ipc = default;
        EntityUid plain = default;
        await Server.WaitPost(() =>
        {
            var coords = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            ipc = SEntMan.SpawnEntity("MobIPC", coords);
            plain = SEntMan.SpawnEntity("MobIPC", coords);
        });

        await Server.WaitAssertion(() =>
        {
            var minds = SEntMan.System<SharedMindSystem>();
            var roles = SEntMan.System<SharedRoleSystem>();
            var psych = SEntMan.System<PsychiatrySystem>();
            var mind = minds.CreateMind(null, "ipc");
            minds.TransferTo(mind.Owner, ipc, mind: mind.Comp);
            Assert.That(psych.TryApplyCyber(ipc, SchizophreniaStage.Simple, "emp", ignoreCooldown: true), Is.True);
            roles.MindAddRole(mind.Owner, "MindRoleChangeling", mind.Comp, silent: true);

            Assert.That(SEntMan.HasComponent<SchizophreniaComponent>(ipc), Is.False);
            Assert.That(psych.TryApplyCyber(ipc, SchizophreniaStage.Latent, "emp"), Is.False);
            Assert.That(psych.TryApplyCyber(ipc, SchizophreniaStage.Latent, "cascade-spike", ignoreCooldown: true, forced: true), Is.True);
            var illness = SEntMan.GetComponent<SchizophreniaComponent>(ipc);
            Assert.That(illness.Kind, Is.EqualTo(PsychiatryIllnessKind.Cyberpsychosis));
            Assert.That(illness.Stage, Is.EqualTo(SchizophreniaStage.Latent));

            Assert.That(psych.TryApplyCyber(plain, SchizophreniaStage.Latent, "ion", harm: true), Is.True);
            var now = Server.ResolveDependency<IGameTiming>().CurTime;
            var tracker = SEntMan.GetComponent<SchizophreniaOnsetTrackerComponent>(plain);
            tracker.NextAllowedOnset = now - TimeSpan.FromSeconds(1);
            Assert.That(psych.TryApplyCyber(plain, SchizophreniaStage.Latent, "emp", harm: true), Is.False);
            tracker.NextHarmStage = now - TimeSpan.FromSeconds(1);
            Assert.That(psych.TryApplyCyber(plain, SchizophreniaStage.Latent, "ion", harm: true), Is.True);
            Assert.That(SEntMan.GetComponent<SchizophreniaComponent>(plain).Stage, Is.EqualTo(SchizophreniaStage.Simple));
        });
    }

    [Test]
    public async Task AghostDoesNotKeepSchizophrenia()
    {
        EntityUid ghost = default;
        await Server.WaitPost(() =>
        {
            var minds = SEntMan.System<SharedMindSystem>();
            var psych = SEntMan.System<PsychiatrySystem>();
            var mind = minds.GetOrCreateMind(ServerSession.UserId);
            minds.TransferTo(mind.Owner, SPlayer, mind: mind.Comp);
            psych.ApplyNew(SPlayer, SchizophreniaStage.Simple, pillForced: false, reason: "test");
            var coords = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            ghost = SEntMan.SpawnEntity("AdminObserver", coords);
            minds.Visit(mind.Owner, ghost, mind.Comp);
        });

        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<SchizophreniaComponent>(SPlayer), Is.True);
            Assert.That(SEntMan.HasComponent<SchizophreniaComponent>(ghost), Is.False);
            Assert.That(SEntMan.HasComponent<GhostComponent>(ghost), Is.True);
        });

        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.System<PsychiatryClientSystem>().TryGetSubject(out _, out _), Is.False);
        });
    }

    [Test]
    public async Task HarmStageCooldownBlocksRapidAsphyxiation()
    {
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.CfgMan.GetCVar(CCCCVars.PsychiatryHarmStageCooldownSec), Is.EqualTo(300f));
            Server.CfgMan.SetCVar(CCCCVars.PsychiatryAsphyxiationChance, 0f);
            var damage = new DamageSpecifier();
            damage.DamageDict["Asphyxiation"] = FixedPoint2.New(40);
            Assert.That(SEntMan.System<DamageableSystem>().TryChangeDamage(SPlayer, damage), Is.True);
            Server.CfgMan.SetCVar(CCCCVars.PsychiatryAsphyxiationChance, 1f);
            var onset = SEntMan.System<PsychiatryOnsetSystem>();
            Assert.That(onset.TryAsphyxiationRoll(SPlayer, 0f), Is.True);
            Assert.That(SEntMan.GetComponent<SchizophreniaComponent>(SPlayer).Stage, Is.EqualTo(SchizophreniaStage.Latent));

            var tracker = SEntMan.GetComponent<SchizophreniaOnsetTrackerComponent>(SPlayer);
            var now = Server.ResolveDependency<IGameTiming>().CurTime;
            tracker.NextAllowedOnset = now - TimeSpan.FromSeconds(1);
            Assert.That(onset.TryAsphyxiationRoll(SPlayer, 0f), Is.False);

            tracker.NextHarmStage = now - TimeSpan.FromSeconds(1);
            Assert.That(onset.TryAsphyxiationRoll(SPlayer, 0f), Is.True);
            Assert.That(SEntMan.GetComponent<SchizophreniaComponent>(SPlayer).Stage, Is.EqualTo(SchizophreniaStage.Simple));
            Server.CfgMan.SetCVar(CCCCVars.PsychiatryAsphyxiationChance, 0.005f);
        });
    }

    [Test]
    public async Task AutoEscalateTakesTenMinutes()
    {
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.CfgMan.GetCVar(CCCCVars.PsychiatryAutoEscalateMinSec), Is.EqualTo(600f));
            Assert.That(Server.CfgMan.GetCVar(CCCCVars.PsychiatryAutoEscalateMaxSec), Is.EqualTo(600f));
        });
    }

    [Test]
    public async Task MedicalGagIsStockedWithoutCargoArbitrage()
    {
        await Server.WaitAssertion(() =>
        {
            Assert.That(ProtoMan.TryIndex(NanoMedInventory, out var nano), Is.True);
            Assert.That(ProtoMan.TryIndex(NanoMedPlusInventory, out var plus), Is.True);
            Assert.That(nano!.StartingInventory.ContainsKey("ClothingMaskMedicalGag"), Is.True);
            Assert.That(plus!.StartingInventory.ContainsKey("ClothingMaskMedicalGag"), Is.True);
            Assert.That(ProtoMan.TryIndex(MedicalRestock, out var product), Is.True);
            Assert.That(product!.Cost, Is.EqualTo(1800));

            var coords = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            var crate = SEntMan.SpawnEntity(product.Product, coords);
            var price = SEntMan.System<PricingSystem>().GetPrice(crate);
            Assert.That(price, Is.LessThanOrEqualTo(product.Cost));
            SEntMan.DeleteEntity(crate);
        });
    }

    [Test]
    public async Task IncipientHasNoSymptomsAndEscalates()
    {
        await Server.WaitPost(() =>
        {
            var psych = SEntMan.System<PsychiatrySystem>();
            var timing = Server.ResolveDependency<IGameTiming>();
            psych.ApplyNew(SPlayer, SchizophreniaStage.Incipient, pillForced: false, reason: "test");
            var schizo = SEntMan.GetComponent<SchizophreniaComponent>(SPlayer);
            Assert.That(schizo.Stage, Is.EqualTo(SchizophreniaStage.Incipient));
            Assert.That(SEntMan.HasComponent<ParacusiaComponent>(SPlayer), Is.False);
            schizo.NextAutoEscalate = timing.CurTime - TimeSpan.FromSeconds(1);
            SEntMan.Dirty(SPlayer, schizo);
        });

        await RunSeconds(1.5f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<SchizophreniaComponent>(SPlayer).Stage, Is.EqualTo(SchizophreniaStage.Latent));
        });
    }

    [Test]
    public async Task HeadTraumaStartsIncipient()
    {
        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.HasComponent<SchizophreniaComponent>(SPlayer), Is.False);
            SEntMan.AddComponent<PsychiatryHeadTraumaComponent>(SPlayer);
            Assert.That(SEntMan.GetComponent<SchizophreniaComponent>(SPlayer).Stage, Is.EqualTo(SchizophreniaStage.Incipient));
        });
    }

    [Test]
    public async Task StasisClearsIllnessAndSensoryFaults()
    {
        await Server.WaitPost(() =>
        {
            var psych = SEntMan.System<PsychiatrySystem>();
            var status = SEntMan.System<StatusEffectsSystem>();
            psych.ApplyNew(SPlayer, SchizophreniaStage.Acute, pillForced: false, reason: "test");
            status.TrySetStatusEffectDuration(SPlayer, "StatusEffectDeaf", null);
            status.TrySetStatusEffectDuration(SPlayer, "StatusEffectMuted", null);
            var eyes = SEntMan.GetComponent<BlindableComponent>(SPlayer);
            SEntMan.System<BlindableSystem>().AdjustEyeDamage((SPlayer, eyes), eyes.MaxDamage);

            var coords = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            var action = SEntMan.SpawnEntity("ActionChangelingStasis", coords);
            var system = SEntMan.System<RegenerativeStasisSystem>();
            system.EnterStasis(action, SPlayer);
            system.ExitStasis(action, SPlayer);
            Assert.That(SEntMan.HasComponent<SchizophreniaComponent>(SPlayer), Is.False);
            SEntMan.DeleteEntity(action);
        });

        await RunTicks(1);

        await Server.WaitAssertion(() =>
        {
            var status = SEntMan.System<StatusEffectsSystem>();
            Assert.That(status.HasStatusEffect(SPlayer, "StatusEffectDeaf"), Is.False);
            Assert.That(status.HasStatusEffect(SPlayer, "StatusEffectMuted"), Is.False);
            Assert.That(SEntMan.GetComponent<BlindableComponent>(SPlayer).EyeDamage, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task NeuroClarityOverdoseMatchesMercury()
    {
        await Server.WaitAssertion(() =>
        {
            Assert.That(ProtoMan.TryIndex(NeuroClarity, out var reagent), Is.True);
            var poison = FixedPoint2.Zero;
            var extra = false;
            var metabolisms = reagent!.Metabolisms;
            Assert.That(metabolisms, Is.Not.Null);
            foreach (var effect in metabolisms![MedicineGroup].Effects)
            {
                if (effect is not HealthChange change || change.Damage == null)
                    continue;
                foreach (var (type, amount) in change.Damage.DamageDict)
                {
                    if (type == "Poison")
                        poison += amount;
                    else
                        extra = true;
                }
            }

            Assert.That(extra, Is.False);
            Assert.That(poison, Is.EqualTo(FixedPoint2.New(1)));
        });
    }

    [Test]
    public async Task DeafListenerDropsNearbySpeech()
    {
        await Server.WaitPost(() =>
        {
            var status = SEntMan.System<StatusEffectsSystem>();
            status.TrySetStatusEffectDuration(SPlayer, "StatusEffectDeaf", null);
            var players = Server.ResolveDependency<IPlayerManager>();
            ICommonSession? session = null;
            foreach (var candidate in players.Sessions)
            {
                if (candidate.AttachedEntity == SPlayer)
                {
                    session = candidate;
                    break;
                }
            }

            Assert.That(session, Is.Not.Null);
            var coords = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            var speaker = SEntMan.SpawnEntity("MobHuman", coords);
            var recipients = new Dictionary<ICommonSession, ChatSystem.ICChatRecipientData>
            {
                [session!] = new(1f, false),
            };
            var ev = new ExpandICChatRecipientsEvent(speaker, 7f, recipients);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ev);
            Assert.That(recipients.ContainsKey(session), Is.False);
        });
    }

    [Test]
    public async Task PolskorovitSteps()
    {
        await Server.WaitAssertion(() =>
        {
            Assert.That(ProtoMan.TryIndex(Polskorovit, out var emote), Is.True);
            Assert.That(emote!.Available, Is.False);
            Assert.That(EmoteAnimation.TryResolve(emote.Steps, out var plan, out var error), Is.True, error);
            Assert.That(plan.Poses.Exists(pose => Math.Abs(pose.Tilt - 20f) < 0.01f), Is.True);
            Assert.That(plan.Poses.Exists(pose => Math.Abs(pose.Tilt + 40f) < 0.01f), Is.True);
            Assert.That(plan.Poses.Exists(pose => pose.Shift.Y > 0.7f), Is.True);
            Assert.That(plan.Poses.Exists(pose => Math.Abs(pose.Face - 90f) < 0.01f), Is.True);
            Assert.That(plan.Poses.Exists(pose => pose.Size.X < 1f && pose.Size.Y > 1f), Is.True);
            Assert.That(plan.Poses[plan.Poses.Count - 1].Tilt, Is.EqualTo(0f).Within(0.01f));
            Assert.That(plan.Poses[plan.Poses.Count - 1].Face, Is.EqualTo(0f).Within(0.01f));
        });
    }
}
