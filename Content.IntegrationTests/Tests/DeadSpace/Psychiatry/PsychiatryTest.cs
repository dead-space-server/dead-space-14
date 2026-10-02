#nullable enable
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.DeadSpace.Psychiatry;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.DeadSpace.Psychiatry;
using Content.Shared.DeadSpace.Skills.Components;
using Content.Shared.DeadSpace.Skills.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.MedicalScanner;
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
            Assert.That(SEntMan.GetComponent<SchizophreniaComponent>(SPlayer).Stage, Is.EqualTo(SchizophreniaStage.Acute));
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
    public async Task LobotomyDropsTwoStages()
    {
        await Server.WaitPost(() =>
        {
            var psych = SEntMan.System<PsychiatrySystem>();
            psych.ApplyNew(SPlayer, SchizophreniaStage.Acute, pillForced: false, reason: "test");
            psych.AdjustStage(SPlayer, -2, "lobotomy-test");
            Assert.That(SEntMan.GetComponent<SchizophreniaComponent>(SPlayer).Stage, Is.EqualTo(SchizophreniaStage.Latent));
        });
    }

    [Test]
    public async Task LobotomyClearsAcute()
    {
        await SpawnTarget("MobHuman");
        await Server.WaitPost(() =>
        {
            Server.CfgMan.SetCVar(CCCCVars.PsychiatryLobotomyFaultChance, 0f);
            var psych = SEntMan.System<PsychiatrySystem>();
            psych.ApplyNew(STarget!.Value, SchizophreniaStage.Acute, pillForced: false, reason: "test");
            Assert.That(SEntMan.HasComponent<SchizophreniaComponent>(STarget!.Value), Is.True);
        });
        await InteractUsing("LobotomyTool");
        await RunSeconds(14f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<SchizophreniaComponent>(STarget!.Value), Is.False);
            Server.CfgMan.SetCVar(CCCCVars.PsychiatryLobotomyFaultChance, 0.30f);
        });
    }

    [Test]
    public async Task LobotomyClearsLatent()
    {
        await SpawnTarget("MobHuman");
        await Server.WaitPost(() =>
        {
            Server.CfgMan.SetCVar(CCCCVars.PsychiatryLobotomyFaultChance, 0f);
            var psych = SEntMan.System<PsychiatrySystem>();
            psych.ApplyNew(STarget!.Value, SchizophreniaStage.Latent, pillForced: false, reason: "test");
        });
        await InteractUsing("LobotomyTool");
        await RunSeconds(14f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<SchizophreniaComponent>(STarget!.Value), Is.False);
            Server.CfgMan.SetCVar(CCCCVars.PsychiatryLobotomyFaultChance, 0.30f);
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
            psych.AdjustStage(SPlayer, -2, "lobotomy-latent");
            Assert.That(SharedPsychiatrySystem.LowerStage(SchizophreniaStage.Latent, 2), Is.EqualTo(SchizophreniaStage.None));
            Assert.That(SEntMan.HasComponent<SchizophreniaComponent>(SPlayer), Is.False);
        });
    }

    [Test]
    public async Task EncephalographRequiresAdvancedTreatment()
    {
        await Server.WaitAssertion(() =>
        {
            var psych = SEntMan.System<PsychiatrySystem>();
            Assert.That(psych.HasAdvancedTreatment(SPlayer), Is.False);

            var skills = SEntMan.EnsureComponent<SkillComponent>(SPlayer);
            skills.Skills[new ProtoId<SkillPrototype>("AdvancedTreatment")] = 1f;
            Assert.That(psych.HasAdvancedTreatment(SPlayer), Is.True);
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
        Assert.That(PsychiatryPattern.ShouldRemapMob(42, 7, SchizophreniaStage.None), Is.False);

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
            Server.CfgMan.SetCVar(CCCCVars.PsychiatryAsphyxiationChance, 0.01f);
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
            psych.AdjustStage(ipc, -2, "hard-reset");
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
            Server.CfgMan.SetCVar(CCCCVars.PsychiatryAsphyxiationChance, 0.01f);
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
}
