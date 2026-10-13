// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.CartridgeLoader;
using Content.Shared.CartridgeLoader;
using Content.Shared.DeadSpace.PdaArcade;
using Content.Shared.GameTicking;

namespace Content.Server.DeadSpace.PdaArcade;

public sealed class PdaArcadeSystem : EntitySystem
{
    private const int MaxPerGame = 20;

    [Dependency] private readonly CartridgeLoaderSystem _loader = default!;

    private readonly List<PdaArcadeEntry> _scores = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PdaArcadeCartridgeComponent, CartridgeMessageEvent>(OnMessage);
        SubscribeLocalEvent<PdaArcadeCartridgeComponent, CartridgeUiReadyEvent>(OnReady);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRestart);
    }

    private void OnRestart(RoundRestartCleanupEvent args)
    {
        _scores.Clear();
    }

    private void OnReady(Entity<PdaArcadeCartridgeComponent> ent, ref CartridgeUiReadyEvent args)
    {
        Push(args.Loader);
    }

    private void OnMessage(Entity<PdaArcadeCartridgeComponent> ent, ref CartridgeMessageEvent args)
    {
        if (args is not PdaArcadeScoreMessage message)
            return;

        if (message.Score <= 0 || message.Score > 1000000 || string.IsNullOrEmpty(message.Game))
            return;

        var name = MetaData(args.Actor).EntityName;
        if (string.IsNullOrWhiteSpace(name))
            name = "—";

        _scores.Add(new PdaArcadeEntry(message.Game, name, message.Score));
        Trim(message.Game);
        Push(GetEntity(message.LoaderUid));
        RefreshOpen();
    }

    private void Trim(string game)
    {
        var kept = 0;
        for (var i = _scores.Count - 1; i >= 0; i--)
        {
            if (_scores[i].Game != game)
                continue;

            kept++;
            if (kept <= MaxPerGame)
                continue;

            _scores.RemoveAt(i);
        }
    }

    private void RefreshOpen()
    {
        var query = EntityQueryEnumerator<PdaArcadeCartridgeComponent, CartridgeComponent>();
        while (query.MoveNext(out _, out _, out var cartridge))
        {
            if (cartridge.LoaderUid is not { } loader)
                continue;

            Push(loader);
        }
    }

    private void Push(EntityUid loader)
    {
        var copy = _scores.ToArray();
        Array.Sort(copy, (a, b) => b.Score.CompareTo(a.Score));
        _loader.UpdateCartridgeUiState(loader, new PdaArcadeUiState { Entries = copy });
    }
}
