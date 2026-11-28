// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Client.DeadSpace.UserInterface.Hologram.Pda;

/// <summary>
/// Style classes of the PDA in the "Dead Space" style. The window chrome classes are also written as literals in
/// <c>PdaWindow.xaml</c>.
/// </summary>
public static class PdaHologramStyleClass
{
    // Window chrome, styled only in the "Dead Space" style on top of the stock PDA classes.
    public const string Shell = "DeadSpacePdaShell";
    public const string Frame = "DeadSpacePdaFrame";
    public const string ScreenFrame = "DeadSpacePdaScreenFrame";
    public const string Screen = "DeadSpacePdaScreen";
    public const string Header = "DeadSpacePdaHeader";
    public const string WindowTitle = "DeadSpacePdaWindowTitle";
    public const string WindowSubtitle = "DeadSpacePdaWindowSubtitle";

    // Home screen and program tiles.
    public const string OwnerName = "DeadSpacePdaOwnerName";
    public const string Meta = "DeadSpacePdaMeta";
    public const string Clock = "DeadSpacePdaClock";
    public const string FieldLabel = "DeadSpacePdaFieldLabel";
    public const string SectionTitle = "DeadSpacePdaSectionTitle";
    public const string AlertText = "DeadSpacePdaAlertText";
    public const string Divider = "DeadSpacePdaDivider";
    public const string CopyRow = "DeadSpacePdaCopyRow";
    public const string Tile = "DeadSpacePdaTile";
    public const string TileLabel = "DeadSpacePdaTileLabel";
}
