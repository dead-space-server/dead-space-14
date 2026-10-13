// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Content.Shared.Corvax.TTS;
using Content.Shared.DeadSpace.TTS;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.DeadSpace.TTS;

[TestFixture]
public sealed class TTSProviderTest
{
    private static readonly ProtoId<TTSVoicePrototype> AlyxVoice = "NTTSAlyx";
    private static readonly ProtoId<TTSVoicePrototype>[] ReservedVoices = ["NTTSGlados", "NTTSAdjutant", "NTTSHermaeusMora"];

    [TestPrototypes]
    private const string Prototypes = @"
- type: ttsVoice
  id: Ds14LegacyTestVoice
  name: tts-voice-name-eugene
  sex: Unsexed
  speaker: ds14_test_speaker

- type: ttsVoice
  id: NTTSDs14TestVoice
  provider: ntts
  name: tts-voice-name-eugene
  sex: Unsexed
  speaker: ds14_test_speaker
";

    [Test]
    public async Task LegacyExportAndBothSelectionsSurviveProviderChanges()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var cfg = server.ResolveDependency<IConfigurationManager>();
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var appearance = server.System<SharedHumanoidAppearanceSystem>();
            var session = server.ResolveDependency<IPlayerManager>().Sessions.Single();
            var alyx = prototypes.Index(AlyxVoice);
            Assert.That(HumanoidCharacterProfile.CanHaveVoice(alyx, Sex.Male), Is.True);
            Assert.That(HumanoidCharacterProfile.CanHaveVoice(alyx, Sex.Female), Is.True);
            Assert.That(HumanoidCharacterProfile.CanHaveVoice(alyx, Sex.Unsexed), Is.True);
            Assert.That(TTSVoices.Resolve(prototypes, TTSVoices.Ntts, "Glados")!.ID, Is.EqualTo("NTTSGlados"));
            Assert.That(TTSVoices.Resolve(prototypes, TTSVoices.Ntts, "Ellie")!.ID, Is.EqualTo("NTTSCirilla"));
            var profile = new HumanoidCharacterProfile { Voice = "Ds14LegacyTestVoice" };
            var export = (MappingDataNode) appearance.ToDataNode(profile);
            ((MappingDataNode) export["profile"]).Remove("ttsVoices");
            using var writer = new StringWriter();
            export.Write(writer);

            cfg.SetCVar(TTSProviderCVars.Provider, TTSVoices.Ntts);
            using var legacyFile = new MemoryStream(Encoding.UTF8.GetBytes(writer.ToString()));
            var imported = appearance.FromStream(legacyFile, session, []);
            Assert.That(imported.Voice, Is.EqualTo(profile.Voice));
            Assert.That(imported.TtsVoices[TTSVoices.Ntts], Is.EqualTo("NTTSDs14TestVoice"));
            Assert.That(imported.GetVoice(TTSVoices.Ntts, prototypes), Is.EqualTo(imported.TtsVoices[TTSVoices.Ntts]));

            imported = imported.WithTtsVoice(TTSVoices.Ntts, "NTTSKleiner");
            var serializer = server.ResolveDependency<ISerializationManager>();
            var roundTrip = serializer.Read<HumanoidProfileExport>(appearance.ToDataNode(imported));
            cfg.SetCVar(TTSProviderCVars.Provider, TTSVoices.Silero);
            roundTrip.Profile.EnsureValid(session, server.InstanceDependencyCollection, []);
            Assert.That(roundTrip.Profile.Voice, Is.EqualTo(profile.Voice));
            Assert.That(roundTrip.Profile.TtsVoices[TTSVoices.Ntts], Is.EqualTo("NTTSKleiner"));

            var fallback = new HumanoidCharacterProfile { Voice = "Eugene" };
            fallback.EnsureValid(session, server.InstanceDependencyCollection, []);
            Assert.That(fallback.Voice, Is.EqualTo("Eugene"));
            Assert.That(fallback.GetVoice(TTSVoices.Ntts, prototypes), Is.EqualTo(TTSVoices.DefaultVoice(TTSVoices.Ntts, fallback.Sex)));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ReservedNttsVoicesRemainForEntitiesAndAreRejectedForCharacters()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Server.WaitAssertion(() =>
        {
            var server = pair.Server;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var session = server.ResolveDependency<IPlayerManager>().Sessions.Single();
            foreach (var id in ReservedVoices)
            {
                var voice = prototypes.Index(id);
                Assert.That(voice.RoundStart, Is.False);
                foreach (var sex in new[] { Sex.Male, Sex.Female, Sex.Unsexed })
                    Assert.That(HumanoidCharacterProfile.CanHaveVoice(voice, sex), Is.False);
                var profile = new HumanoidCharacterProfile().WithSex(Sex.Female).WithVoice("Maiev").WithTtsVoice(TTSVoices.Ntts, id.Id);
                var oldVoice = profile.Voice;
                profile.EnsureValid(session, server.InstanceDependencyCollection, []);
                Assert.That(profile.TtsVoices[TTSVoices.Ntts], Is.Not.EqualTo(id.Id));
                Assert.That(profile.Voice, Is.EqualTo(oldVoice));
                Assert.That(TTSVoices.Resolve(prototypes, TTSVoices.Ntts, id.Id)!.ID, Is.EqualTo(id.Id));
            }
            Assert.That(TTSVoices.Resolve(prototypes, TTSVoices.Ntts, "Glados")!.ID, Is.EqualTo("NTTSGlados"));
            Assert.That(TTSVoices.Resolve(prototypes, TTSVoices.Ntts, "Adjutant")!.ID, Is.EqualTo("NTTSAdjutant"));
            Assert.That(TTSVoices.Resolve(prototypes, TTSVoices.Ntts, "Hermaeus")!.ID, Is.EqualTo("NTTSHermaeusMora"));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EntityVoiceAndCloneRetainBothProviders()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.ResolveDependency<IEntityManager>();
            var appearance = pair.Server.System<SharedHumanoidAppearanceSystem>();
            var source = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var clone = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var sourceAppearance = entities.GetComponent<HumanoidAppearanceComponent>(source);
            appearance.SetTTSVoice(source, "Eugene", sourceAppearance, new Dictionary<string, string> { [TTSVoices.Ntts] = "NTTSKleiner" });
            appearance.CloneAppearance(source, clone);
            var tts = entities.GetComponent<TTSComponent>(clone);
            Assert.That(TTSVoices.GetVoice(tts, TTSVoices.Silero), Is.EqualTo("Eugene"));
            Assert.That(TTSVoices.GetVoice(tts, TTSVoices.Ntts), Is.EqualTo("NTTSKleiner"));
            entities.DeleteEntity(source);
            entities.DeleteEntity(clone);
        });
        await pair.CleanReturnAsync();
    }
}
