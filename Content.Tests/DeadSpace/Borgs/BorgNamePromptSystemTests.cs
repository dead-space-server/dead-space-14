// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Borgs;
using NUnit.Framework;

namespace Content.Tests.DeadSpace.Borgs;

/// <summary>
/// Юнит-тесты валидации и санитизации имени киборга.
/// Значения идентификаторов (например, "Си-1234", "Кс-9876") здесь являются тестовыми примерами (mock-данными),
/// тогда как в реальной игре они берутся динамически из NameIdentifierComponent сущности.
/// </summary>
[TestFixture]
public sealed class BorgNamePromptSystemTests
{
    private const int MaxNameLength = 32;
    private const string SampleSiliconId = "Си-1234";
    private const string SampleXenoborgId = "Кс-9876";

    [TestCase("")]
    [TestCase(" ")]
    [TestCase("   \t \n")]
    public void EmptyOrWhitespaceName_ReturnsTooShort(string input)
    {
        var result = BorgNamePromptSystem.CleanAndValidateName(input, SampleSiliconId, MaxNameLength, out var error);

        Assert.That(result, Is.Null);
        Assert.That(error, Is.EqualTo("borg-name-prompt-too-short"));
    }

    [TestCase("a")]
    [TestCase("я")]
    [TestCase(" 1 ")]
    public void SingleCharacterName_ReturnsTooShort(string input)
    {
        var result = BorgNamePromptSystem.CleanAndValidateName(input, SampleSiliconId, MaxNameLength, out var error);

        Assert.That(result, Is.Null);
        Assert.That(error, Is.EqualTo("borg-name-prompt-too-short"));
    }

    [TestCase("Бип", "Бип")]
    [TestCase("Валли", "Валли")]
    [TestCase("Робокоп", "Робокоп")]
    [TestCase("  Ева-01  ", "Ева-01")]
    public void ValidNames_AreTrimmedAndAccepted(string input, string expected)
    {
        var result = BorgNamePromptSystem.CleanAndValidateName(input, SampleSiliconId, MaxNameLength, out var error);

        Assert.That(result, Is.EqualTo(expected));
        Assert.That(error, Is.Null);
    }

    [Test]
    public void DuplicateIdentifierWithParentheses_IsStripped()
    {
        var input = "Валли (Си-1234)";
        var result = BorgNamePromptSystem.CleanAndValidateName(input, SampleSiliconId, MaxNameLength, out var error);

        Assert.That(result, Is.EqualTo("Валли"));
        Assert.That(error, Is.Null);
    }

    [Test]
    public void DuplicateIdentifierWithoutParentheses_IsStripped()
    {
        var input = "Валли Си-1234";
        var result = BorgNamePromptSystem.CleanAndValidateName(input, SampleSiliconId, MaxNameLength, out var error);

        Assert.That(result, Is.EqualTo("Валли"));
        Assert.That(error, Is.Null);
    }

    [Test]
    public void XenoborgIdentifier_IsStrippedCorrectly()
    {
        // Проверяем работу с другим типом борга (ксеноборг)
        var input = "Жнец (Кс-9876)";
        var result = BorgNamePromptSystem.CleanAndValidateName(input, SampleXenoborgId, MaxNameLength, out var error);

        Assert.That(result, Is.EqualTo("Жнец"));
        Assert.That(error, Is.Null);
    }

    [Test]
    public void OnlyIdentifierEntered_ReturnsTooShort()
    {
        var input = "(Си-1234)";
        var result = BorgNamePromptSystem.CleanAndValidateName(input, SampleSiliconId, MaxNameLength, out var error);

        Assert.That(result, Is.Null);
        Assert.That(error, Is.EqualTo("borg-name-prompt-too-short"));
    }

    [Test]
    public void ExcessiveLength_ReturnsTooLong()
    {
        var input = new string('A', MaxNameLength + 1);
        var result = BorgNamePromptSystem.CleanAndValidateName(input, SampleSiliconId, MaxNameLength, out var error);

        Assert.That(result, Is.Null);
        Assert.That(error, Is.EqualTo("borg-name-prompt-too-long"));
    }

    [Test]
    public void NullIdentifier_AcceptsValidName()
    {
        var input = "Бип";
        var result = BorgNamePromptSystem.CleanAndValidateName(input, null, MaxNameLength, out var error);

        Assert.That(result, Is.EqualTo("Бип"));
        Assert.That(error, Is.Null);
    }
}
