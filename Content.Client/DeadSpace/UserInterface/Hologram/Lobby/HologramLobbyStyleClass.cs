// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Client.DeadSpace.UserInterface.Hologram.Lobby;

/// <summary>
/// Style classes of the lobby and the main menu. They only carry rules in the "Dead Space" style
/// (<see cref="HologramLobbySheetlet"/>), always next to the DS14 class they refine; the other styles ignore them.
/// </summary>
public static class HologramLobbyStyleClass
{
    /// <summary>The outer lobby shell: dissolves, so its panels stand as separate projections.</summary>
    public const string Shell = "DeadSpaceHoloLobbyShell";

    /// <summary>A lobby panel: projected frame, corner brackets, cut corner.</summary>
    public const string Panel = "DeadSpaceHoloLobbyPanel";

    /// <summary>The header bar of a lobby panel.</summary>
    public const string Header = "DeadSpaceHoloLobbyHeader";

    /// <summary>The caption in a lobby panel header: small, bright.</summary>
    public const string Title = "DeadSpaceHoloLobbyTitle";

    /// <summary>The round countdown box.</summary>
    public const string Status = "DeadSpaceHoloLobbyStatus";

    public const string StatusTitle = "DeadSpaceHoloLobbyStatusTitle";

    /// <summary>The countdown itself: large monospace digits.</summary>
    public const string StatusTime = "DeadSpaceHoloLobbyStatusTime";

    /// <summary>The character preview box.</summary>
    public const string Inset = "DeadSpaceHoloLobbyInset";

    /// <summary>The character summary under the preview.</summary>
    public const string CharacterName = "DeadSpaceHoloLobbyCharacterName";

    /// <summary>The big "МЁРТВЫЙ КОСМОС" title of the hologram lobby.</summary>
    public const string BigTitle = "DeadSpaceHoloLobbyBigTitle";

    /// <summary>The round line under the big title.</summary>
    public const string Subtitle = "DeadSpaceHoloLobbySubtitle";

    /// <summary>A small muted caption (row labels, header tags).</summary>
    public const string Caption = "DeadSpaceHoloLobbyCaption";

    /// <summary>A bright read-out value (players online).</summary>
    public const string Value = "DeadSpaceHoloLobbyValue";
}
