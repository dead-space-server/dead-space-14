// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Content.Server.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NUnit.Framework;

namespace Content.Tests.DeadSpace.TTS;

[TestFixture]
public sealed class TtsVoicesMigrationTest
{
    [Test]
    public async Task UpgradingLegacyDatabasePreservesCharacter()
    {
        var options = new DbContextOptionsBuilder<SqliteServerDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using var db = new SqliteServerDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.MigrateAsync();
        db.Profile.Add(new Profile
        {
            CharacterName = "Старый персонаж",
            FlavorText = "Описание",
            Age = 25,
            Sex = "Male",
            Gender = "Male",
            Species = "Human",
            Voice = "Eugene",
            HairName = "Bald",
            FacialHairName = "Shaved",
            HairColor = "#000000",
            FacialHairColor = "#000000",
            EyeColor = "#000000",
            SkinColor = "#000000",
            HairGradientColor = "#000000",
            Preference = new Preference { UserId = Guid.NewGuid(), AdminOOCColor = "#FFFFFF" },
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.That((await db.Profile.SingleAsync()).TtsVoices, Is.Null);

        var migrations = db.Database.GetMigrations().ToArray();
        var previous = migrations[Array.FindIndex(migrations, name => name.EndsWith("_TtsVoices")) - 1];
        await db.GetService<IMigrator>().MigrateAsync(previous);
        db.ChangeTracker.Clear();
        await db.Database.MigrateAsync();
        var profile = await db.Profile.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(profile.CharacterName, Is.EqualTo("Старый персонаж"));
            Assert.That(profile.FlavorText, Is.EqualTo("Описание"));
            Assert.That(profile.Voice, Is.EqualTo("Eugene"));
            Assert.That(profile.TtsVoices!.RootElement.GetProperty("silero").GetString(), Is.EqualTo("Eugene"));
            Assert.That(profile.Age, Is.EqualTo(25));
        });

        using var voices = JsonDocument.Parse("{\"silero\":\"Eugene\",\"ntts\":\"NTTSKleiner\"}");
        profile.TtsVoices = voices;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        profile = await db.Profile.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(profile.TtsVoices!.RootElement.GetProperty("silero").GetString(), Is.EqualTo("Eugene"));
            Assert.That(profile.TtsVoices!.RootElement.GetProperty("ntts").GetString(), Is.EqualTo("NTTSKleiner"));
        });

        profile.TtsVoices = null;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        profile = await db.Profile.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(profile.TtsVoices, Is.Null);
            Assert.That(profile.Voice, Is.EqualTo("Eugene"));
        });
    }
}
