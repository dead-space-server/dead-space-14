// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.PdaArcade;

public static class PdaArcadeGame
{
    public const string Snake = "snake";
    public const string Tiles = "tiles";
    public const string Blocks = "blocks";
    public const string Rocks = "rocks";
    public const string Flap = "flap";
    public const string Match = "match";
}

[Serializable, NetSerializable]
public struct PdaArcadeEntry
{
    public string Game;
    public string Name;
    public int Score;

    public PdaArcadeEntry(string game, string name, int score)
    {
        Game = game;
        Name = name;
        Score = score;
    }
}

[Serializable, NetSerializable]
public sealed class PdaArcadeUiState : BoundUserInterfaceState
{
    public PdaArcadeEntry[] Entries = Array.Empty<PdaArcadeEntry>();
}

[Serializable, NetSerializable]
public sealed class PdaArcadeScoreMessage : CartridgeMessageEvent
{
    public string Game = "";
    public int Score;
}
