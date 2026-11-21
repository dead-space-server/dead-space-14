// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Server.DeadSpace.Borgs;
using Content.Server.Silicons.AiBorgControl;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.Tests.DeadSpace.Borgs;

[TestFixture]
public sealed class BorgNamePromptIntegrationTest
{
    private const string BorgPrototype = "BorgChassisGeneric";

    [Test]
    public async Task BorgPromptTriggeredOnPlayerAttachAndDeduplicates()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var playerMan = server.PlayerMan;
        var testMap = await pair.CreateTestMap();
        var promptSys = server.System<BorgNamePromptSystem>();

        await server.WaitAssertion(() =>
        {
            var session = playerMan.Sessions.Single();

            // 1. Проверяем автоматическое срабатывание при PlayerAttachedEvent
            var borg = entMan.SpawnEntity(BorgPrototype, testMap.GridCoords);
            Assert.That(entMan.HasComponent<BorgChassisComponent>(borg), Is.True);

            // Прикрепляем игрока к шасси
            playerMan.SetAttachedEntity(session, borg);

            // BorgNamePromptComponent должен автоматически появиться и зафиксировать игрока
            Assert.That(entMan.TryGetComponent<BorgNamePromptComponent>(borg, out var promptComp), Is.True);
            Assert.That(promptComp!.PromptedUsers.Contains(session.UserId), Is.True);

            // 2. Повторная попытка вызова TryPromptName для того же игрока должна вернуть false (защита от спама/реконнекта)
            var chassis = entMan.GetComponent<BorgChassisComponent>(borg);
            Assert.That(promptSys.TryPromptName(borg, chassis, session), Is.False);

            // 3. Киборг под удаленным контролем ИИ не должен опрашивать имя
            var aiBorg = entMan.SpawnEntity(BorgPrototype, testMap.GridCoords);
            entMan.AddComponent<AiControlledBorgComponent>(aiBorg);
            var aiChassis = entMan.GetComponent<BorgChassisComponent>(aiBorg);
            Assert.That(promptSys.TryPromptName(aiBorg, aiChassis, session), Is.False);

            // 4. Проверяем наличие всех ключей локализации на сервере
            var loc = server.Resolve<ILocalizationManager>();
            Assert.That(loc.HasString("borg-name-prompt-dialog-title"), Is.True);
            Assert.That(loc.HasString("borg-name-prompt-dialog-prompt"), Is.True);
            Assert.That(loc.HasString("borg-name-prompt-dialog-prompt-with-id"), Is.True);
            Assert.That(loc.HasString("borg-name-prompt-too-short"), Is.True);
            Assert.That(loc.HasString("borg-name-prompt-too-long"), Is.True);
            Assert.That(loc.HasString("borg-name-prompt-success"), Is.True);

            // Очистка сущностей
            playerMan.SetAttachedEntity(session, null);
            entMan.DeleteEntity(borg);
            entMan.DeleteEntity(aiBorg);
        });

        await pair.CleanReturnAsync();
    }
}
