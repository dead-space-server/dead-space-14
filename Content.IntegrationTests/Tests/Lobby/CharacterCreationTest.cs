using System.Linq;
using System.Threading;
using Content.Client.Lobby;
using Content.IntegrationTests.Fixtures;
using Content.Server.Database;
using Content.Server.Preferences.Managers;
using Content.Shared.CCVar;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Robust.Server.Player;
using Robust.Client.State;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

namespace Content.IntegrationTests.Tests.Lobby;

[TestFixture]
[TestOf(typeof(ClientPreferencesManager))]
[TestOf(typeof(ServerPreferencesManager))]
public sealed class CharacterCreationTest : GameTest
{
    public override PoolSettings PoolSettings => new() { InLobby = true };

    [Test]
    public async Task CreateDeleteCreateTest()
    {
        var pair = Pair;
        var server = pair.Server;
        var client = pair.Client;
        var user = pair.Client.User!.Value;
        var clientPrefManager = client.Resolve<IClientPreferencesManager>();
        var serverPrefManager = server.Resolve<IServerPreferencesManager>();

        Assert.That(client.Resolve<IStateManager>().CurrentState, Is.TypeOf<LobbyState>());
        await client.WaitPost(() => clientPrefManager.SelectCharacter(0));
        await pair.RunTicksSync(5);

        var clientCharacters = clientPrefManager.Preferences?.Characters;
        Assert.That(clientCharacters, Is.Not.Null);
        Assert.That(clientCharacters, Has.Count.EqualTo(1));

        HumanoidCharacterProfile profile = null;
        await client.WaitPost(() =>
        {
            profile = HumanoidCharacterProfile.Random();
            clientPrefManager.CreateCharacter(profile);
        });
        await pair.RunTicksSync(5);

        clientCharacters = clientPrefManager.Preferences?.Characters;
        Assert.That(clientCharacters, Is.Not.Null);
        Assert.That(clientCharacters, Has.Count.EqualTo(2));
        AssertEqual(clientCharacters[1], profile);

        await PoolManager.WaitUntil(server, () => serverPrefManager.GetPreferences(user).Characters.Count == 2, maxTicks: 60);

        var serverCharacters = serverPrefManager.GetPreferences(user).Characters;
        Assert.That(serverCharacters, Has.Count.EqualTo(2));
        AssertEqual(serverCharacters[1], profile);

        await client.WaitAssertion(() => clientPrefManager.DeleteCharacter(1));
        await pair.RunTicksSync(5);
        Assert.That(clientPrefManager.Preferences?.Characters.Count, Is.EqualTo(1));
        await PoolManager.WaitUntil(server, () => serverPrefManager.GetPreferences(user).Characters.Count == 1, maxTicks: 60);
        Assert.That(serverPrefManager.GetPreferences(user).Characters.Count, Is.EqualTo(1));

        await client.WaitIdleAsync();

        await client.WaitAssertion(() =>
        {
            profile = HumanoidCharacterProfile.Random();
            clientPrefManager.CreateCharacter(profile);
        });
        await pair.RunTicksSync(5);

        clientCharacters = clientPrefManager.Preferences?.Characters;
        Assert.That(clientCharacters, Is.Not.Null);
        Assert.That(clientCharacters, Has.Count.EqualTo(2));
        AssertEqual(clientCharacters[1], profile);

        await PoolManager.WaitUntil(server, () => serverPrefManager.GetPreferences(user).Characters.Count == 2, maxTicks: 60);
        serverCharacters = serverPrefManager.GetPreferences(user).Characters;
        Assert.That(serverCharacters, Has.Count.EqualTo(2));
        AssertEqual(serverCharacters[1], profile);
    }

    // DS14-start
    [Test]
    public async Task InaccessibleCharactersAreSplitAndDeletableTest()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var user = pair.Client.User!.Value;
        var clientPrefManager = client.Resolve<IClientPreferencesManager>();
        var serverPrefManager = server.Resolve<IServerPreferencesManager>();
        var db = server.Resolve<IServerDbManager>();
        var cfg = server.Resolve<IConfigurationManager>();
        var playerManager = server.Resolve<IPlayerManager>();
        var clientNetManager = client.Resolve<IClientNetManager>();
        var username = string.Empty;

        var accessibleProfile = ProfileWithName("Алексей Тестов");
        var inaccessibleProfile = ProfileWithName("Борис Закрытов");

        await PoolManager.WaitUntil(client, () =>
            clientPrefManager.ServerDataLoaded &&
            clientPrefManager.Preferences != null,
            maxTicks: 60);
        await PoolManager.WaitUntil(server, () => serverPrefManager.TryGetCachedPreferences(user, out _), maxTicks: 60);

        await server.WaitPost(() =>
        {
            cfg.SetCVar(CCVars.GameMaxCharacterSlots, 2);
            db.SaveCharacterSlotAsync(user, accessibleProfile, 0).Wait();
            db.SaveCharacterSlotAsync(user, inaccessibleProfile, 1).Wait();
            db.SaveSelectedCharacterIndexAsync(user, 1).Wait();
            username = playerManager.Sessions.Single().Name;
        });

        var seededPrefs = await db.GetPlayerPreferencesAsync(user, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(seededPrefs, Is.Not.Null);
            Assert.That(seededPrefs!.SelectedCharacterIndex, Is.EqualTo(1));
            Assert.That(seededPrefs.Characters.Keys, Is.EquivalentTo(new[] { 0, 1 }));
            Assert.That(((HumanoidCharacterProfile) seededPrefs.Characters[0]).Name, Is.EqualTo(accessibleProfile.Name));
            Assert.That(((HumanoidCharacterProfile) seededPrefs.Characters[1]).Name, Is.EqualTo(inaccessibleProfile.Name));
        });

        await client.WaitPost(() => clientNetManager.ClientDisconnect("For testing"));
        await pair.RunTicksSync(20);

        await server.WaitAssertion(() =>
        {
            Assert.That(playerManager.PlayerCount, Is.EqualTo(0));
            cfg.SetCVar(CCVars.GameMaxCharacterSlots, 1);
        });

        client.SetConnectTarget(server);
        await client.WaitPost(() => clientNetManager.ClientConnect(null!, 0, username));
        await pair.RunTicksSync(20);

        await PoolManager.WaitUntil(client, () =>
            clientPrefManager.ServerDataLoaded &&
            clientPrefManager.Settings!.MaxCharacterSlots == 1 &&
            clientPrefManager.Preferences!.InaccessibleCharacters.Count == 1,
            maxTicks: 60);

        var clientPrefs = clientPrefManager.Preferences!;
        Assert.Multiple(() =>
        {
            Assert.That(clientPrefs.SelectedCharacterIndex, Is.EqualTo(0));
            Assert.That(clientPrefs.Characters.Keys, Is.EquivalentTo(new[] { 0 }));
            Assert.That(clientPrefs.InaccessibleCharacters.Keys, Is.EquivalentTo(new[] { 1 }));
            Assert.That(((HumanoidCharacterProfile) clientPrefs.Characters[0]).Name, Is.EqualTo(accessibleProfile.Name));
            Assert.That(((HumanoidCharacterProfile) clientPrefs.InaccessibleCharacters[1]).Name, Is.EqualTo(inaccessibleProfile.Name));
        });

        await PoolManager.WaitUntil(server, () =>
        {
            var serverPrefs = serverPrefManager.GetPreferences(user);
            return serverPrefs.SelectedCharacterIndex == 0 &&
                   serverPrefs.Characters.Count == 1 &&
                   serverPrefs.InaccessibleCharacters.Count == 1;
        }, maxTicks: 60);

        var rawPrefs = await db.GetPlayerPreferencesAsync(user, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(rawPrefs, Is.Not.Null);
            Assert.That(rawPrefs!.SelectedCharacterIndex, Is.EqualTo(0));
            Assert.That(rawPrefs.Characters.Keys, Is.EquivalentTo(new[] { 0, 1 }));
        });

        await client.WaitPost(() => clientPrefManager.DeleteCharacter(1));
        await pair.RunTicksSync(5);
        await PoolManager.WaitUntil(server, () => !serverPrefManager.GetPreferences(user).InaccessibleCharacters.ContainsKey(1), maxTicks: 60);

        rawPrefs = await db.GetPlayerPreferencesAsync(user, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(rawPrefs, Is.Not.Null);
            Assert.That(rawPrefs!.Characters.Keys, Is.EquivalentTo(new[] { 0 }));
        });

        await client.WaitPost(() => clientNetManager.ClientDisconnect("For testing"));
        await pair.RunTicksSync(20);

        await server.WaitAssertion(() =>
        {
            Assert.That(playerManager.PlayerCount, Is.EqualTo(0));
            cfg.SetCVar(CCVars.GameMaxCharacterSlots, 2);
        });

        client.SetConnectTarget(server);
        await client.WaitPost(() => clientNetManager.ClientConnect(null!, 0, username));
        await pair.RunTicksSync(20);

        await PoolManager.WaitUntil(client, () =>
            clientPrefManager.ServerDataLoaded &&
            clientPrefManager.Settings!.MaxCharacterSlots == 2 &&
            clientPrefManager.Preferences!.Characters.Count == 1,
            maxTicks: 60);

        clientPrefs = clientPrefManager.Preferences!;
        Assert.Multiple(() =>
        {
            Assert.That(clientPrefs.Characters.Keys, Is.EquivalentTo(new[] { 0 }));
            Assert.That(clientPrefs.InaccessibleCharacters, Is.Empty);
        });

        await pair.CleanReturnAsync();
    }

    private static HumanoidCharacterProfile ProfileWithName(string name)
    {
        return new HumanoidCharacterProfile
        {
            Name = name
        };
    }
    // DS14-end

    private void AssertEqual(ICharacterProfile clientCharacter, HumanoidCharacterProfile b)
    {
        if (clientCharacter.MemberwiseEquals(b))
            return;

        if (clientCharacter is not HumanoidCharacterProfile a)
        {
            Assert.Fail($"Not a {nameof(HumanoidCharacterProfile)}");
            return;
        }

        Assert.Multiple(() =>
        {
            Assert.That(a.Name, Is.EqualTo(b.Name));
            Assert.That(a.Age, Is.EqualTo(b.Age));
            Assert.That(a.Sex, Is.EqualTo(b.Sex));
            Assert.That(a.Gender, Is.EqualTo(b.Gender));
            Assert.That(a.Species, Is.EqualTo(b.Species));
            Assert.That(a.PreferenceUnavailable, Is.EqualTo(b.PreferenceUnavailable));
            Assert.That(a.SpawnPriority, Is.EqualTo(b.SpawnPriority));
            Assert.That(a.FlavorText, Is.EqualTo(b.FlavorText));
            Assert.That(a.JobPriorities, Is.EquivalentTo(b.JobPriorities));
            Assert.That(a.AntagPreferences, Is.EquivalentTo(b.AntagPreferences));
            Assert.That(a.TraitPreferences, Is.EquivalentTo(b.TraitPreferences));
            Assert.That(a.Loadouts, Is.EquivalentTo(b.Loadouts));
            AssertEqual(a.Appearance, b.Appearance);
            Assert.Fail("Profile not equal");
        });
    }

    private void AssertEqual(HumanoidCharacterAppearance a, HumanoidCharacterAppearance b)
    {
        if (a.MemberwiseEquals(b))
            return;

        Assert.That(a.HairStyleId, Is.EqualTo(b.HairStyleId));
        Assert.That(a.HairColor, Is.EqualTo(b.HairColor));
        Assert.That(a.FacialHairStyleId, Is.EqualTo(b.FacialHairStyleId));
        Assert.That(a.FacialHairColor, Is.EqualTo(b.FacialHairColor));
        Assert.That(a.EyeColor, Is.EqualTo(b.EyeColor));
        Assert.That(a.SkinColor, Is.EqualTo(b.SkinColor));
        Assert.That(a.Markings, Is.EquivalentTo(b.Markings));
        Assert.Fail("Appearance not equal");
    }
}
