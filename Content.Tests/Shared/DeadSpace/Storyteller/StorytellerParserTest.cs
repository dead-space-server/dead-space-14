// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Shared.DeadSpace.Storyteller;
using Content.Shared.Humanoid;
using NUnit.Framework;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Tests.Shared.DeadSpace.Storyteller;

[TestFixture]
public sealed class StorytellerParserTest : ContentUnitTest
{
    private StoryLexicon _lexicon;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        IoCManager.Resolve<ISerializationManager>().Initialize();
        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        prototypes.Initialize();

        var folder = FindStoryPrototypes();
        prototypes.LoadString(File.ReadAllText(Path.Combine(folder, "language.yml")));
        prototypes.LoadString(File.ReadAllText(Path.Combine(folder, "mood.yml")));
        prototypes.ResolveResults();

        _lexicon = Lexicon(prototypes);
    }

    private static string FindStoryPrototypes()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "Resources", "Prototypes", "_DeadSpace", "Storyteller");
            if (Directory.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        Assert.Fail("Storyteller prototypes were not found.");
        return "";
    }

    private static StoryLexicon Lexicon(IPrototypeManager prototypes)
    {
        var lexicon = new StoryLexicon();
        lexicon.SetLanguage(prototypes.Index<StoryLanguagePrototype>("Russian"));
        foreach (var emote in prototypes.EnumeratePrototypes<StoryEmotePrototype>())
            lexicon.AddEmote(emote.ID, emote.Stems, emote.Phrases);
        foreach (var light in prototypes.EnumeratePrototypes<StoryLightPrototype>())
            lexicon.AddLight(light.ID, light.Words, light.Stems, light.Phrases);

        lexicon.Add(lexicon.Locations, new[] { "бар" }, "Bar");
        lexicon.Add(lexicon.Locations, new[] { "медотсек" }, "Medbay");
        lexicon.Add(lexicon.Locations, new[] { "космос", "космосе" }, "Space");
        lexicon.Add(lexicon.Kinds, new[] { "унатх", "ящер" }, "Reptilian");
        lexicon.Add(lexicon.Kinds, new[] { "некроморф", "некроморфа" }, "Necromorph");
        lexicon.Add(lexicon.Roles, new[] { "бармен", "бармена" }, "Bartender");
        lexicon.Add(lexicon.Roles, new[] { "врач", "доктор" }, "Doctor");
        lexicon.Add(lexicon.Roles, new[] { "яо", "ядерный", "оперативник" }, "Syndicate");
        lexicon.Add(lexicon.Items, new[] { "пиво", "пива" }, "Beer");
        lexicon.Add(lexicon.Items, new[] { "пистолет", "пистолета" }, "Pistol");
        return lexicon;
    }

    private List<StoryCue> Parse(string line, params StoryActorRef[] actors)
    {
        return StorytellerParser.Parse(line, "Рассказчик", actors, _lexicon);
    }

    [Test]
    public void ANamedCharacterWalksIntoTheBar()
    {
        var cues = Parse("Иван Кувалда заходит в бар, а только задник поставился");

        Assert.That(cues, Has.Count.EqualTo(2));
        Assert.That(cues[0].Kind, Is.EqualTo(StoryCueKind.Location));
        Assert.That(cues[0].Id, Is.EqualTo("Bar"));
        Assert.That(cues[1].Kind, Is.EqualTo(StoryCueKind.Enter));
        Assert.That(cues[1].Actor, Is.EqualTo("Иван Кувалда"));
        Assert.That(cues[1].Sex, Is.EqualTo(Sex.Male));
    }

    [Test]
    public void SpeciesJobAndSexComeFromTheLine()
    {
        var cues = Parse("Девушка-врач Алиса входит в медотсек");
        var enter = cues.Single(c => c.Kind == StoryCueKind.Enter);

        Assert.That(cues[0].Id, Is.EqualTo("Medbay"));
        Assert.That(enter.Actor, Is.EqualTo("Алиса"));
        Assert.That(enter.Role, Is.EqualTo("Doctor"));
        Assert.That(enter.Sex, Is.EqualTo(Sex.Female));

        var lizard = Parse("Появляется унатх-бармен Гиви").Single();
        Assert.That(lizard.Kind, Is.EqualTo(StoryCueKind.Enter));
        Assert.That(lizard.Actor, Is.EqualTo("Гиви"));
        Assert.That(lizard.Id, Is.EqualTo("Reptilian"));
        Assert.That(lizard.Role, Is.EqualTo("Bartender"));
    }

    [Test]
    public void AFeminineVerbMakesTheNewcomerAWoman()
    {
        var enter = Parse("Кувалда зашла в бар").Single(c => c.Kind == StoryCueKind.Enter);

        Assert.That(enter.Sex, Is.EqualTo(Sex.Female));
    }

    [Test]
    public void TwoCharactersInteractInOneLine()
    {
        var cues = Parse("Алиса подходит к Гиви и обнимает его",
            new StoryActorRef("Гиви", Sex.Male),
            new StoryActorRef("Алиса", Sex.Female));

        Assert.That(cues, Has.Count.EqualTo(2));
        Assert.That(cues[0].Kind, Is.EqualTo(StoryCueKind.Move));
        Assert.That(cues[0].Move, Is.EqualTo(StoryMove.Target));
        Assert.That(cues[0].Target, Is.EqualTo("Гиви"));
        Assert.That(cues[1].Kind, Is.EqualTo(StoryCueKind.Hug));
        Assert.That(cues[1].Actor, Is.EqualTo("Алиса"));
        Assert.That(cues[1].Target, Is.EqualTo("Гиви"));
    }

    [Test]
    public void GivingNamesTheItemAndTheDeclinedReceiver()
    {
        var cues = Parse("Гиви даёт Алисе пиво",
            new StoryActorRef("Алиса", Sex.Female),
            new StoryActorRef("Гиви", Sex.Male));
        var give = cues.Single();

        Assert.That(give.Kind, Is.EqualTo(StoryCueKind.Give));
        Assert.That(give.Actor, Is.EqualTo("Гиви"));
        Assert.That(give.Target, Is.EqualTo("Алиса"));
        Assert.That(give.Id, Is.EqualTo("Beer"));
    }

    [Test]
    public void AnUnknownTargetIsBroughtOnStageFirst()
    {
        var cues = Parse("Иван бьёт Сторожа", new StoryActorRef("Иван", Sex.Male));

        Assert.That(cues, Has.Count.EqualTo(2));
        Assert.That(cues[0].Kind, Is.EqualTo(StoryCueKind.Enter));
        Assert.That(cues[0].Actor, Is.EqualTo("Сторож"));
        Assert.That(cues[1].Kind, Is.EqualTo(StoryCueKind.Hit));
        Assert.That(cues[1].Target, Is.EqualTo("Сторож"));
    }

    [Test]
    public void TakingAGunThenShooting()
    {
        var cues = Parse("Иван достаёт пистолет и стреляет в некроморфа",
            new StoryActorRef("Иван", Sex.Male),
            new StoryActorRef("Некроморф", Sex.Unsexed));

        Assert.That(cues.Select(c => c.Kind), Is.EqualTo(new[] { StoryCueKind.Hold, StoryCueKind.Shoot }));
        Assert.That(cues[0].Id, Is.EqualTo("Pistol"));
        Assert.That(cues[1].Target, Is.EqualTo("Некроморф"));
    }

    [Test]
    public void QuotedSpeechAndPronouns()
    {
        var say = Parse("Гиви: «Добро пожаловать!»", new StoryActorRef("Гиви", Sex.Male)).Single();
        Assert.That(say.Kind, Is.EqualTo(StoryCueKind.Say));
        Assert.That(say.Actor, Is.EqualTo("Гиви"));
        Assert.That(say.Text, Is.EqualTo("Добро пожаловать!"));

        var laugh = Parse("Она смеётся", new StoryActorRef("Гиви", Sex.Male), new StoryActorRef("Алиса", Sex.Female)).Single();
        Assert.That(laugh.Kind, Is.EqualTo(StoryCueKind.Emote));
        Assert.That(laugh.Actor, Is.EqualTo("Алиса"));
        Assert.That(laugh.Id, Is.EqualTo("laugh"));
    }

    [Test]
    public void TheNarratorStepsInAndFallsDead()
    {
        var cues = Parse("Я падаю замертво");

        Assert.That(cues[0].Kind, Is.EqualTo(StoryCueKind.Enter));
        Assert.That(cues[0].Text, Is.EqualTo("self"));
        Assert.That(cues[1].Kind, Is.EqualTo(StoryCueKind.Pose));
        Assert.That(cues[1].Pose, Is.EqualTo(StoryPose.Dead));
    }

    [Test]
    public void ReceiverNamesReturnToTheNominative()
    {
        Assert.That(StorytellerParser.Nominative("Алисе", true, _lexicon), Is.EqualTo("Алиса"));
        Assert.That(StorytellerParser.Nominative("Ивану", true, _lexicon), Is.EqualTo("Иван"));
        Assert.That(StorytellerParser.Nominative("Алису", false, _lexicon), Is.EqualTo("Алиса"));
        Assert.That(StorytellerParser.Nominative("Ивана", false, _lexicon), Is.EqualTo("Иван"));
    }

    [Test]
    public void AJobGivenAfterEnteringRedressesTheNewcomer()
    {
        var cues = Parse("В бар заходит Манго, он был Врачом");

        Assert.That(cues.Select(c => c.Kind), Is.EqualTo(new[] { StoryCueKind.Location, StoryCueKind.Enter, StoryCueKind.Look }));
        Assert.That(cues[1].Actor, Is.EqualTo("Манго"));
        Assert.That(cues[2].Actor, Is.EqualTo("Манго"));
        Assert.That(cues[2].Role, Is.EqualTo("Doctor"));
    }

    [Test]
    public void ASharedSurnameIsANewCharacter()
    {
        var cues = Parse("В бар заходит Иоанна Кувалда, она была ЯО", new StoryActorRef("Иван Кувалда", Sex.Male));
        var enter = cues.Single(c => c.Kind == StoryCueKind.Enter);
        var look = cues.Single(c => c.Kind == StoryCueKind.Look);

        Assert.That(enter.Actor, Is.EqualTo("Иоанна Кувалда"));
        Assert.That(enter.Sex, Is.EqualTo(Sex.Female));
        Assert.That(look.Actor, Is.EqualTo("Иоанна Кувалда"));
        Assert.That(look.Role, Is.EqualTo("Syndicate"));
    }

    [Test]
    public void ReportedSpeechStaysWithTheSpeaker()
    {
        var cues = Parse("Манго говорит Кувалде, чтобы тот шел чинить ПР",
            new StoryActorRef("Иван Кувалда", Sex.Male),
            new StoryActorRef("Манго", Sex.Male));

        Assert.That(cues.Select(c => c.Kind), Is.EqualTo(new[] { StoryCueKind.Face, StoryCueKind.Say }));
        Assert.That(cues[0].Target, Is.EqualTo("Иван Кувалда"));
        Assert.That(cues[1].Actor, Is.EqualTo("Манго"));
        Assert.That(cues[1].Text, Is.EqualTo("Иван Кувалда, чтобы тот шел чинить ПР"));
    }

    [Test]
    public void AJobWordFindsTheCharacterWithThatJob()
    {
        var say = Parse("А бармен ему отвечает «Алло»",
            new StoryActorRef("Гиви", Sex.Male, "Bartender", "Reptilian"),
            new StoryActorRef("Иван", Sex.Male)).Single(c => c.Kind == StoryCueKind.Say);

        Assert.That(say.Actor, Is.EqualTo("Гиви"));
        Assert.That(say.Text, Is.EqualTo("Алло"));
    }

    [Test]
    public void NightAndPowerCutSetTheLight()
    {
        var cues = Parse("Наступила ночь, и свет погас");

        Assert.That(cues.Where(c => c.Kind == StoryCueKind.Light).Select(c => c.Id), Is.EqualTo(new[] { "Night", "Dark" }));
    }

    [Test]
    public void LongLinesAndPlainWordsDoNotStageAnything()
    {
        Assert.That(Parse(new string('а', StorytellerParser.MaxLineLength + 1)), Is.Empty);
        Assert.That(Parse("и тогда все замолчали."), Is.Empty);
        Assert.That(Parse("Иван легко улыбается", new StoryActorRef("Иван", Sex.Male)).Single().Id, Is.EqualTo("smile"));
    }
}
