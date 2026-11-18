// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Client.DeadSpace.RoundEnd.Credits;
using Content.Server.DeadSpace.RoundEnd.Credits;
using Content.Server.GameTicking;
using Content.Server.RoundEnd;
using Content.Shared.CCVar;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.DeadSpace.RoundEnd.Credits;
using Content.Shared.Players;
using Robust.Client.UserInterface;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.DeadSpace.RoundEnd;

[TestFixture]
[NonParallelizable]
[TestOf(typeof(RoundEndCreditsSystem))]
public sealed class RoundEndCreditsTests
{
    [Test]
    public async Task EndRoundRollsCreditsAndFreezesMaps()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });
        var server = pair.Server;
        var client = pair.Client;
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var players = server.ResolveDependency<IPlayerManager>();
        var ticker = server.System<GameTicker>();
        var roundEnd = server.System<RoundEndSystem>();
        var credits = server.System<RoundEndCreditsSystem>();
        var stats = server.System<RoundEndCreditsStatsSystem>();

        await server.WaitPost(() =>
        {
            cfg.SetCVar(CCCCVars.RoundEndCreditsEnabled, true);
            cfg.SetCVar(CCCCVars.RoundEndCreditsFadeTime, 0.1f);
            cfg.SetCVar(CCCCVars.RoundEndCreditsVoteTime, 0f);
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
            Assert.That(credits.GetShuttleRoundEndDelay(TimeSpan.FromMinutes(5)),
                Is.EqualTo(TimeSpan.FromSeconds(cfg.GetCVar(CCCCVars.RoundEndCreditsShuttleDelay))));

            var mind = players.Sessions.Select(s => s.GetMind()).FirstOrDefault(m => m != null);
            Assert.That(mind, Is.Not.Null);
            stats.AddStat(mind!.Value, RoundEndCreditsStat.Slips, 7);

            roundEnd.EndRound();
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PostRound));
        });

        await pair.RunTicksSync((int) Math.Ceiling(timing.TickRate * 0.5f));

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(credits.CreditsStarted, Is.True);
                Assert.That(credits.Credits, Is.Not.Null);
                Assert.That(credits.Credits!.ContentDuration, Is.GreaterThan(RoundEndCreditsOverlay.IntroSeconds));
                Assert.That(credits.Credits.Nominations.Any(a => a.Prototype == "CreditsFunSlips" && a.Value >= 7), Is.True);
                Assert.That(credits.Credits.Outcome.Headline, Is.EqualTo(RoundEndCreditsHeadline.Calm));
            });

            var maps = server.EntMan.AllEntityQueryEnumerator<MapComponent>();
            var count = 0;
            while (maps.MoveNext(out _, out var map))
            {
                count++;
                Assert.That(map.MapPaused, Is.True);
            }

            Assert.That(count, Is.GreaterThan(0));
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PostRound));
        });

        var controller = client.ResolveDependency<IUserInterfaceManager>().GetUIController<RoundEndCreditsUIController>();
        var showing = false;
        for (var i = 0; i < timing.TickRate * 5 && !showing; i++)
        {
            await pair.RunTicksSync(1);
            await client.WaitPost(() => showing = controller.ShowingCredits);
        }

        Assert.That(showing, Is.True);

        await server.WaitPost(() =>
        {
            cfg.SetCVar(CCCCVars.RoundEndCreditsEnabled, false);
            cfg.SetCVar(CCCCVars.RoundEndCreditsFadeTime, CCCCVars.RoundEndCreditsFadeTime.DefaultValue);
            cfg.SetCVar(CCCCVars.RoundEndCreditsVoteTime, CCCCVars.RoundEndCreditsVoteTime.DefaultValue);
            ticker.RestartRound();
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DisabledCreditsKeepRestartTime()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            DummyTicker = false,
        });
        var server = pair.Server;
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var credits = server.System<RoundEndCreditsSystem>();
        var roundEnd = server.System<RoundEndSystem>();

        await server.WaitAssertion(() =>
        {
            var fallback = TimeSpan.FromSeconds(42);
            Assert.That(credits.GetShuttleRoundEndDelay(fallback), Is.EqualTo(fallback));

            roundEnd.EndRound();
            Assert.That(credits.Credits, Is.Null);
            Assert.That(credits.GetPlannedRestartDelay(), Is.Null);
            Assert.That(cfg.GetCVar(CCVars.RoundRestartTime), Is.GreaterThan(0));
        });

        await pair.CleanReturnAsync();
    }
}
