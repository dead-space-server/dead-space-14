// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.CartridgeLoader;
using Content.Shared.Eui;
using Robust.Shared.Network;
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
    public const string Deck = "deck";
    public const string Road = "road";
    public const string Cross = "cross";
    public const string Ships = "ships";
    public const string Pong = "pong";
    public const string Chess = "chess";
    public const string Checks = "checks";

    public static readonly string[] Together =
    {
        Cross,
        Ships,
        Pong,
        Chess,
        Checks,
    };
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
    public PdaArcadePerson[] Nearby = Array.Empty<PdaArcadePerson>();
    public bool InMatch;
    public string MatchGame = "";
    public string MatchBoard = "";
    public bool MatchYours;
    public bool MatchOver;
    public bool Rolling;
    public bool MineBottom;
    public int YourDie;
    public int TheirDie;
}

[Serializable, NetSerializable]
public struct PdaArcadePerson
{
    public NetEntity Entity;
    public string Name = "";

    public PdaArcadePerson(NetEntity entity, string name)
    {
        Entity = entity;
        Name = name;
    }
}

[Serializable, NetSerializable]
public sealed class PdaArcadeScoreMessage : CartridgeMessageEvent
{
    public string Game = "";
    public int Score;
}

[Serializable, NetSerializable]
public sealed class PdaArcadeNearbyMessage : CartridgeMessageEvent
{
}

[Serializable, NetSerializable]
public sealed class PdaArcadeInviteMessage : CartridgeMessageEvent
{
    public string Game = "";
    public NetEntity Target;
}

[Serializable, NetSerializable]
public sealed class PdaArcadeMoveMessage : CartridgeMessageEvent
{
    public string Move = "";
}

[Serializable, NetSerializable]
public sealed class PdaArcadeLeaveMessage : CartridgeMessageEvent
{
}

[Serializable, NetSerializable]
public sealed class PdaArcadeAgainMessage : CartridgeMessageEvent
{
}

[Serializable, NetSerializable]
public sealed class PdaInviteState : EuiStateBase
{
    public string Text = "";
}

[Serializable, NetSerializable]
public sealed class PdaInviteChoiceMessage : EuiMessageBase
{
    public bool Accept;
}
