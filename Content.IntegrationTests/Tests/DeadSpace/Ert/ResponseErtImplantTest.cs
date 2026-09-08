using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.Eui;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.DeadSpace.ERT;
using Content.Server.DeadSpace.ERT.Components;
using Content.Server.DeadSpace.ERTCall;
using Content.Server.Roles;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Access.Systems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.ERT;
using Content.Shared.GameTicking;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Pinpointer;
using Content.Shared.Shuttles.Components;
using Content.Shared.Station.Components;
using Content.Shared.Storage;
using Robust.Client.UserInterface;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.DeadSpace.Ert;

[TestFixture]
public sealed class ResponseErtImplantTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    private EntityUid _mind;

    [TearDown]
    public async Task CleanupResponseRequests()
    {
        // The dummy ticker skips round cleanup when pooled servers restart.
        await Server.WaitPost(() => SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent()));
    }

    private async Task Prepare()
    {
        await AddAtmosphere();
        await Server.WaitPost(() =>
        {
            var minds = Server.System<SharedMindSystem>();
            var mind = minds.CreateMind(ServerSession.UserId, "Patient");
            _mind = mind.Owner;
            minds.TransferTo(mind, SPlayer);
        });
    }

    private void AddStation()
    {
        var station = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
        SEntMan.AddComponent(MapData.Grid, new StationMemberComponent { Station = station });
    }

    private Entity<ResponseErtImplantComponent> AddImplant(string prototype = "TrackingImplantCriticalForce")
    {
        var implant = Server.System<SharedSubdermalImplantSystem>().AddImplant(SPlayer, prototype);
        Assert.That(implant, Is.Not.Null);
        return (implant!.Value, SEntMan.GetComponent<ResponseErtImplantComponent>(implant.Value));
    }

    private bool ActionEnabled(EntityUid implant)
    {
        var action = SEntMan.GetComponent<SubdermalImplantComponent>(implant).Action;
        Assert.That(action, Is.Not.Null);
        return SEntMan.GetComponent<ActionComponent>(action!.Value).Enabled;
    }

    [Test]
    public async Task ClientActionOpensConfirmationAndRecoveryClosesIt()
    {
        await Prepare();
        Entity<ResponseErtImplantComponent> implant = default;
        NetEntity action = default;
        await Server.WaitPost(() =>
        {
            AddStation();
            var critical = Server.System<MobThresholdSystem>().GetThresholdForState(SPlayer, MobState.Critical);
            Server.System<DamageableSystem>().SetDamage(SPlayer, new DamageSpecifier { DamageDict = { ["Blunt"] = critical + 1 } });
            implant = AddImplant();
            Assert.That(ActionEnabled(implant), Is.True);
            action = SEntMan.GetNetEntity(SEntMan.GetComponent<SubdermalImplantComponent>(implant).Action!.Value);
        });
        await RunTicks(10);
        await Client.WaitPost(() => CEntMan.RaisePredictiveEvent(new RequestPerformActionEvent(action)));
        await RunTicks(10);
        await Client.WaitAssertion(() =>
        {
            var window = Client.Resolve<IUserInterfaceManager>().WindowRoot.Children.OfType<YesNoWindow>().Single();
            Assert.That(window.MessageLabel.Size.X, Is.LessThanOrEqualTo(440));
            window.Close();
        });
        await RunTicks(10);
        await Server.WaitAssertion(() => Assert.That(implant.Comp.Used, Is.False, "Closing the prompt preserves the call"));
        await Client.WaitPost(() => CEntMan.RaisePredictiveEvent(new RequestPerformActionEvent(action)));
        await RunTicks(10);
        await Client.WaitAssertion(() => Assert.That(
            Client.Resolve<IUserInterfaceManager>().WindowRoot.Children.OfType<YesNoWindow>(), Has.Exactly(1).Items));
        await Server.WaitPost(() => Server.System<DamageableSystem>().SetDamage(SPlayer, new DamageSpecifier()));
        await RunTicks(10);
        await Client.WaitAssertion(() => Assert.That(
            Client.Resolve<IUserInterfaceManager>().WindowRoot.Children.OfType<YesNoWindow>(), Is.Empty));
        await Server.WaitAssertion(() => Assert.That(implant.Comp.Used, Is.False));
    }

    [Test]
    public async Task RequestsValidateHealthOwnerExtractionAndAntagonistStatus()
    {
        await Prepare();
        await Server.WaitAssertion(() =>
        {
            var calls = Server.System<ResponseErtImplantSystem>();
            var implants = Server.System<SharedSubdermalImplantSystem>();
            var states = Server.System<MobStateSystem>();
            var implant = AddImplant();
            Assert.That(ActionEnabled(implant), Is.False);
            Assert.That(calls.TryCallHelp(implant, SPlayer, ServerSession, out _), Is.False);

            states.ChangeMobState(SPlayer, MobState.Critical);
            Assert.That(ActionEnabled(implant), Is.True);
            Assert.That(calls.TryCallHelp(implant, SPlayer, ServerSession, out _), Is.False, "Off-station calls are rejected");
            AddStation();

            states.ChangeMobState(SPlayer, MobState.Alive);
            Assert.That(ActionEnabled(implant), Is.False);
            Assert.That(calls.TryCallHelp(implant, SPlayer, ServerSession, out _), Is.False, "Recovery invalidates confirmation");
            states.ChangeMobState(SPlayer, MobState.Critical);

            var other = SEntMan.SpawnEntity("MobHuman", MapData.GridCoords);
            Server.PlayerMan.SetAttachedEntity(ServerSession, other);
            Assert.That(calls.TryCallHelp(implant, SPlayer, ServerSession, out _), Is.False, "Confirmation cannot follow the session to another body");
            Server.PlayerMan.SetAttachedEntity(ServerSession, SPlayer);

            var container = SEntMan.GetComponent<ImplantedComponent>(SPlayer).ImplantContainer;
            Assert.That(Server.System<SharedContainerSystem>().Remove(implant.Owner, container), Is.True);
            Assert.That(SEntMan.GetComponent<SubdermalImplantComponent>(implant).Action, Is.Null);
            Assert.That(calls.TryCallHelp(implant, SPlayer, ServerSession, out _), Is.False);
            implants.ForceImplant(SPlayer, implant.Owner);
            Assert.That(ActionEnabled(implant), Is.True, "Implanting an already critical patient grants an enabled action");

            var roles = Server.System<RoleSystem>();
            roles.MindAddRole(_mind, "MindRoleTraitor", silent: true);
            Assert.That(calls.TryCallHelp(implant, SPlayer, ServerSession, out _), Is.False);
            Assert.That(implant.Comp.Used, Is.False);
            Assert.That(roles.MindRemoveRole(_mind, "MindRoleTraitor"), Is.True);

            Assert.That(Server.System<ErtResponseSystem>().TryCallErt("CriticalForce", null, out _,
                toPay: false, needCooldown: false, needWarn: false, pinpointerTarget: SPlayer), Is.True);
            Assert.That(calls.TryCallHelp(implant, SPlayer, ServerSession, out _), Is.False, "An existing request rejects a duplicate");
            Assert.That(implant.Comp.Used, Is.False, "Rejected calls do not consume the implant");
        });
    }

    [Test]
    public async Task AcceptedCallSpawnsEquippedRespondersAndCannotBeRepeated()
    {
        await Prepare();
        await Server.WaitAssertion(() =>
        {
            AddStation();
            Server.System<MobStateSystem>().ChangeMobState(SPlayer, MobState.Critical);
            var implant = AddImplant();
            var calls = Server.System<ResponseErtImplantSystem>();
            Assert.That(calls.TryCallHelp(implant, SPlayer, ServerSession, out _), Is.True);
            Assert.That(implant.Comp.Used, Is.True);
            Assert.That(ActionEnabled(implant), Is.False);
            Assert.That(calls.TryCallHelp(implant, SPlayer, ServerSession, out _), Is.False);

            var container = SEntMan.GetComponent<ImplantedComponent>(SPlayer).ImplantContainer;
            Assert.That(Server.System<SharedContainerSystem>().Remove(implant.Owner, container), Is.True);
            Server.System<SharedSubdermalImplantSystem>().ForceImplant(SPlayer, implant.Owner);
            Assert.That(ActionEnabled(implant), Is.False, "Reimplanting cannot recharge a used implant");
            var replacement = AddImplant();
            Assert.That(calls.TryCallHelp(replacement, SPlayer, ServerSession, out _), Is.False, "A fresh implant cannot bypass the per-player limit");
            Assert.That(replacement.Comp.Used, Is.False);
        });

        await RunSeconds(241);
        await Server.WaitAssertion(() =>
        {
            var rule = SEntMan.EntityQuery<ErtSpawnRuleComponent>().Single(r => r.Team == "CriticalForce");
            Assert.That(rule.ShuttleEntity, Is.Not.Null);
            var shuttle = rule.ShuttleEntity!.Value;
            Assert.That(SEntMan.GetComponent<TransformComponent>(shuttle).LocalPosition, Is.EqualTo(Vector2.Zero), "The mapping camera must start at the shuttle");
            var map = SEntMan.GetComponent<TransformComponent>(shuttle).MapID;
            var responders = new List<Entity<ErtStaffComponent>>();
            var query = SEntMan.EntityQueryEnumerator<ErtStaffComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var staff, out var xform))
            {
                if (xform.MapID == map)
                    responders.Add((uid, staff));
            }
            Assert.That(responders, Has.Count.EqualTo(2));
            var inventory = Server.System<InventorySystem>();
            foreach (var responder in responders)
            {
                Assert.That(responder.Comp.CallReason, Is.Not.Null.And.Not.Empty);
                Assert.That(inventory.TryGetSlotEntity(responder, "back", out var back), Is.True);
                Assert.That(SEntMan.GetComponent<StorageComponent>(back!.Value).Container.ContainedEntities, Has.Count.EqualTo(6), "All evacuation supplies must fit in the backpack");
                Assert.That(inventory.TryGetSlotEntity(responder, "belt", out var belt), Is.True);
                Assert.That(SEntMan.GetComponent<StorageComponent>(belt!.Value).Container.ContainedEntities, Has.Count.EqualTo(5), "All belt equipment must fit");
                Assert.That(inventory.TryGetSlotEntity(responder, "pocket1", out var pointer), Is.True);
                Assert.That(SEntMan.GetComponent<PinpointerComponent>(pointer!.Value).Target, Is.EqualTo(SPlayer));
            }

            var response = Server.System<ErtResponseSystem>();
            Assert.That(response.GetBalance(), Is.EqualTo(8), "Medical calls do not spend station ERT points");
            Assert.That(response.TryCallErt("CriticalForce", null, out _,
                toPay: false, needCooldown: false, needWarn: false, pinpointerTarget: SPlayer), Is.False, "The patient's deployed team prevents a duplicate");
            SEntMan.DeleteEntity(shuttle);
            Assert.That(response.TryCallErt("CriticalForce", null, out _,
                toPay: false, needCooldown: false, needWarn: false, pinpointerTarget: SPlayer), Is.True, "Departure releases the patient's deployment slot");
        });
    }

    [TestCase("CriticalForce")]
    [TestCase("SyndicateEvacuation")]
    public async Task ConcurrentCallsDeploySeparateTeamsForEachPatient(string team)
    {
        await Prepare();
        var patients = new Dictionary<EntityUid, string>();
        await Server.WaitAssertion(() =>
        {
            patients.Add(SPlayer, "Recover the first patient");
            patients.Add(SEntMan.SpawnEntity("MobHuman", MapData.GridCoords), "Recover the second patient");
            var response = Server.System<ErtResponseSystem>();
            foreach (var (patient, reason) in patients)
            {
                Assert.That(response.TryCallErt(team, null, out var failure,
                    toPay: false, needCooldown: false, needWarn: false,
                    pinpointerTarget: patient, callReason: reason), Is.True, failure);
            }

            Assert.That(response.TryCallErt(team, null, out _,
                toPay: false, needCooldown: false, needWarn: false, pinpointerTarget: SPlayer), Is.False, "Pending requests still reject duplicate patients");

            Assert.That(response.TryCallErt("Amber", null, out _,
                toPay: false, needCooldown: false, needWarn: false, pinpointerTarget: SPlayer), Is.True);
            Assert.That(response.TryCallErt("Amber", null, out _,
                toPay: false, needCooldown: false, needWarn: false, pinpointerTarget: patients.Keys.Last()), Is.False, "Ordinary ERT requests retain their shared limit");
        });

        await RunSeconds(241);
        await Server.WaitAssertion(() =>
        {
            var rules = SEntMan.EntityQuery<ErtSpawnRuleComponent>().Where(r => r.Team == team).ToList();
            Assert.That(rules, Has.Count.EqualTo(2));
            Assert.That(rules.Select(r => r.PinpointerTarget), Is.EquivalentTo(patients.Keys));
            Assert.That(rules.Select(r => r.ShuttleEntity).Distinct().Count(), Is.EqualTo(2));
            Assert.That(rules.Select(r => SEntMan.GetComponent<TransformComponent>(r.ShuttleEntity!.Value).MapID).Distinct().Count(), Is.EqualTo(2));
            foreach (var rule in rules)
            {
                var shuttle = rule.ShuttleEntity!.Value;
                var responders = 0;
                var query = SEntMan.EntityQueryEnumerator<ErtStaffComponent, TransformComponent>();
                while (query.MoveNext(out var uid, out var staff, out var xform))
                {
                    if (xform.GridUid != shuttle)
                        continue;

                    responders++;
                    Assert.That(staff.CallReason, Is.EqualTo(patients[rule.PinpointerTarget!.Value]));
                    Assert.That(Server.System<InventorySystem>().TryGetSlotEntity(uid, "pocket1", out var pointer), Is.True);
                    Assert.That(SEntMan.GetComponent<PinpointerComponent>(pointer!.Value).Target, Is.EqualTo(rule.PinpointerTarget));
                }
                Assert.That(responders, Is.EqualTo(2));
            }

            var response = Server.System<ErtResponseSystem>();
            Assert.That(response.TryCallErt(team, null, out _,
                toPay: false, needCooldown: false, needWarn: false, pinpointerTarget: SPlayer), Is.False, "A deployed team still rejects duplicate patients");
            var nextPatient = SEntMan.SpawnEntity("MobHuman", MapData.GridCoords);
            Assert.That(response.TryCallErt(team, null, out _,
                toPay: false, needCooldown: false, needWarn: false, pinpointerTarget: nextPatient), Is.True, "Deployed teams do not block another patient's request");

            var firstRule = rules.Single(r => r.PinpointerTarget == SPlayer);
            SEntMan.DeleteEntity(firstRule.ShuttleEntity);
            var remainingRule = rules.Single(r => r.PinpointerTarget != SPlayer);
            Assert.That(SEntMan.EntityExists(remainingRule.ShuttleEntity), Is.True, "The other patient's shuttle remains deployed");
            Assert.That(response.TryCallErt(team, null, out _,
                toPay: false, needCooldown: false, needWarn: false, pinpointerTarget: remainingRule.PinpointerTarget), Is.False);
        });
    }

    [Test]
    public async Task SyndicateContractRequiresOnlyTheOrdinaryTraitorRole()
    {
        await Prepare();
        await Server.WaitAssertion(() =>
        {
            AddStation();
            Server.System<MobStateSystem>().ChangeMobState(SPlayer, MobState.Critical);
            var implant = AddImplant("TrackingImplantSyndicateEvacuation");
            var calls = Server.System<ResponseErtImplantSystem>();
            var roles = Server.System<RoleSystem>();
            Assert.That(calls.TryCallHelp(implant, SPlayer, ServerSession, out _), Is.False, "Crew cannot use the agent contract");

            foreach (var role in new[] { "MindRoleTraitorSleeper", "MindRoleTraitorReinforcement", "MindRoleTraitorUltra", "MindRoleNukeops" })
            {
                roles.MindAddRole(_mind, role, silent: true);
                Assert.That(calls.TryCallHelp(implant, SPlayer, ServerSession, out _), Is.False, role);
                Assert.That(roles.MindRemoveRole(_mind, role), Is.True);
            }

            roles.MindAddRole(_mind, "MindRoleTraitor", silent: true);
            roles.MindAddRole(_mind, "MindRoleNukeops", silent: true);
            Assert.That(calls.TryCallHelp(implant, SPlayer, ServerSession, out _), Is.False, "An additional antagonist role invalidates the contract");
            Assert.That(implant.Comp.Used, Is.False);
            Assert.That(roles.MindRemoveRole(_mind, "MindRoleNukeops"), Is.True);
            var accepted = calls.TryCallHelp(implant, SPlayer, ServerSession, out var reason);
            Assert.That(accepted, Is.True, reason);
            Assert.That(implant.Comp.Used, Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ExtractionOnlyEndsCharactersAboardOnFinalDeparture(bool patientAboard)
    {
        await Prepare();
        await Server.WaitAssertion(() =>
        {
            AddStation();
            Server.System<RoleSystem>().MindAddRole(_mind, "MindRoleTraitor", silent: true);
            Server.System<MobStateSystem>().ChangeMobState(SPlayer, MobState.Critical);
            var implant = AddImplant("TrackingImplantSyndicateEvacuation");
            Assert.That(Server.System<ResponseErtImplantSystem>().TryCallHelp(implant, SPlayer, ServerSession, out _), Is.True);
            Server.System<MobStateSystem>().ChangeMobState(SPlayer, MobState.Alive);
        });

        await RunSeconds(241);
        EntityUid shuttle = default;
        EntityUid console = default;
        EntityUid escort = default;
        await Server.WaitAssertion(() =>
        {
            var rule = SEntMan.EntityQuery<ErtSpawnRuleComponent>().Single(r => r.Team == "SyndicateEvacuation");
            shuttle = rule.ShuttleEntity!.Value;
            Assert.That(SEntMan.GetComponent<TransformComponent>(shuttle).LocalPosition, Is.EqualTo(Vector2.Zero));
            var consoles = SEntMan.EntityQueryEnumerator<ErtComputerShuttleComponent, TransformComponent>();
            while (consoles.MoveNext(out var uid, out _, out var xform))
            {
                if (xform.GridUid == shuttle)
                    console = uid;
            }
            Assert.That(console.IsValid(), Is.True);
            var responders = new List<EntityUid>();
            var query = SEntMan.EntityQueryEnumerator<ErtStaffComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var staff, out var xform))
            {
                if (xform.GridUid != shuttle)
                    continue;

                responders.Add(uid);
                Assert.That(staff.CallReason, Is.Not.Null.And.Not.Empty);
                var inventory = Server.System<InventorySystem>();
                Assert.That(inventory.TryGetSlotEntity(uid, "pocket1", out var pointer), Is.True);
                Assert.That(SEntMan.GetComponent<PinpointerComponent>(pointer!.Value).Target, Is.EqualTo(SPlayer));
                Assert.That(inventory.TryGetSlotEntity(uid, "back", out var back), Is.True);
                Assert.That(SEntMan.GetComponent<StorageComponent>(back!.Value).Container.ContainedEntities, Has.Count.EqualTo(6));
                Assert.That(inventory.TryGetSlotEntity(uid, "belt", out var belt), Is.True);
                Assert.That(SEntMan.GetComponent<StorageComponent>(belt!.Value).Container.ContainedEntities, Has.Count.EqualTo(5));
                Assert.That(Server.System<AccessReaderSystem>().IsAllowed(uid, console), Is.True, "Both responders can finalize extraction");
            }
            Assert.That(responders, Has.Count.EqualTo(2));
            escort = responders[0];
            Assert.That(Server.System<AccessReaderSystem>().IsAllowed(SPlayer, console), Is.False, "The patient has no departure-console access");

            Server.System<SharedTransformSystem>().SetCoordinates(SPlayer, new EntityCoordinates(shuttle, new Vector2(0.5f, 4.5f)));
            var shuttles = Server.System<ShuttleSystem>();
            Assert.That(shuttles.CanFTL(shuttle, out _), Is.True);
            var shuttleTransform = SEntMan.GetComponent<TransformComponent>(shuttle);
            shuttles.FTLToCoordinates(shuttle, SEntMan.GetComponent<ShuttleComponent>(shuttle),
                shuttleTransform.Coordinates, shuttleTransform.LocalRotation, startupTime: 1, hyperspaceTime: 6);
        });

        await RunSeconds(75);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(shuttle), Is.True);
            Assert.That(SEntMan.HasComponent<FTLComponent>(shuttle), Is.False, "The ordinary flight must have completed");
            Assert.That(SEntMan.GetComponent<MindComponent>(_mind).OwnedEntity, Is.EqualTo(SPlayer), "Ordinary FTL is not final extraction");
            if (!patientAboard)
                Server.System<SharedTransformSystem>().SetCoordinates(SPlayer, MapData.GridCoords);

            SEntMan.EventBus.RaiseLocalEvent(console, new ErtComputerShuttleUiButtonPressedMessage(ErtComputerShuttleUiButton.Evacuation));
            Assert.That(SEntMan.GetComponent<ErtComputerShuttleComponent>(console).IsEvacuation, Is.True);
        });

        await RunSeconds(90);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(shuttle), Is.False, "Final extraction removes the shuttle");
            Assert.That(SEntMan.EntityExists(escort), Is.False, "Responders leave with their shuttle");
            Assert.That(SEntMan.EntityExists(SPlayer), Is.EqualTo(!patientAboard));
            var owned = SEntMan.GetComponent<MindComponent>(_mind).OwnedEntity;
            if (patientAboard)
                Assert.That(owned, Is.Not.EqualTo(SPlayer), "An extracted agent cannot return to their body");
            else
                Assert.That(owned, Is.EqualTo(SPlayer), "Departure without the patient must not end their character remotely");

        });
    }
}
