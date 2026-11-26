// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.DeadSpace.Stylesheets;
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Fonts;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Pda;

/// <summary>
/// The PDA in the "Dead Space" style: projected window chrome instead of the coloured case, the home screen and the
/// program tiles. Chrome rules pair the stock PDA classes with DS-only ones, so the turret console and research cards
/// that share the stock classes keep their look.
/// </summary>
[CommonSheetlet]
public sealed class PdaHologramSheetlet : Sheetlet<NanotrasenStylesheet>
{
    /// <summary>Same corner brackets as the other hologram windows; corners are square.</summary>
    private const float WindowBracket = 14f;

    public override StyleRule[] GetRules(NanotrasenStylesheet sheet, object config)
    {
        // The home screen and tiles are shown only in the "Dead Space" style, so their rules can always be present.
        var clockFont = sheet.BaseFont.GetFont(20);
        var divider = DeadSpaceStyleBoxes.Flat(DeadSpaceStylePalette.BorderDark);

        var copyRow = new StyleBoxEmpty();
        var copyRowHover = DeadSpaceStyleBoxes.Flat(DeadSpaceStylePalette.CyanSelection.WithAlpha(0.18f));

        var tile = DeadSpaceStyleBoxes.Holo(
            DeadSpaceStylePalette.Control,
            DeadSpaceStylePalette.BorderControl,
            horizontalMargin: 6,
            verticalMargin: 8);
        var tileHover = DeadSpaceStyleBoxes.Holo(
            DeadSpaceStylePalette.ControlHover,
            DeadSpaceStylePalette.HoverOutline,
            horizontalMargin: 6,
            verticalMargin: 8);
        var tilePressed = DeadSpaceStyleBoxes.Holo(
            DeadSpaceStylePalette.ControlPressed,
            DeadSpaceStylePalette.PressedOutline,
            horizontalMargin: 6,
            verticalMargin: 8);
        var tileDisabled = DeadSpaceStyleBoxes.Holo(
            DeadSpaceStylePalette.ControlDisabled,
            DeadSpaceStylePalette.BorderDisabled,
            horizontalMargin: 6,
            verticalMargin: 8);

        var rules = new List<StyleRule>
        {
            E<Label>()
                .Class(PdaHologramStyleClass.WindowTitle)
                .Font(sheet.BaseFont.GetFont(12, FontKind.Bold))
                .FontColor(DeadSpaceStylePalette.Heading),
            E<Label>()
                .Class(PdaHologramStyleClass.WindowSubtitle)
                .Font(sheet.BaseFont.GetFont(10))
                .FontColor(DeadSpaceStylePalette.TextMuted),
            E<Label>()
                .Class(PdaHologramStyleClass.OwnerName)
                .Font(sheet.BaseFont.GetFont(16, FontKind.Bold))
                .FontColor(DeadSpaceStylePalette.Text),
            E<Label>()
                .Class(PdaHologramStyleClass.Meta)
                .Font(sheet.BaseFont.GetFont(10))
                .FontColor(DeadSpaceStylePalette.TextMuted),
            E<Label>()
                .Class(PdaHologramStyleClass.FieldLabel)
                .Font(sheet.BaseFont.GetFont(10))
                .FontColor(DeadSpaceStylePalette.TextMuted),
            E<Label>()
                .Class(PdaHologramStyleClass.SectionTitle)
                .Font(sheet.BaseFont.GetFont(10, FontKind.Bold))
                .FontColor(DeadSpaceStylePalette.Heading),
            E<Label>()
                .Class(PdaHologramStyleClass.Clock)
                .Font(clockFont)
                .FontColor(DeadSpaceStylePalette.CyanBright),
            // The colour comes from the station alert level.
            E<Label>()
                .Class(PdaHologramStyleClass.AlertText)
                .Font(sheet.BaseFont.GetFont(10, FontKind.Bold)),
            E<Label>()
                .Class(PdaHologramStyleClass.TileLabel)
                .Font(sheet.BaseFont.GetFont(12))
                .FontColor(DeadSpaceStylePalette.Text),

            E<PanelContainer>().Class(PdaHologramStyleClass.Divider).Panel(divider),

            // Rows that copy their value; more specific than the global bare ContainerButton surface.
            E<ContainerButton>().Class(PdaHologramStyleClass.CopyRow).PseudoNormal().Box(copyRow).Modulate(Color.White),
            E<ContainerButton>().Class(PdaHologramStyleClass.CopyRow).PseudoHovered().Box(copyRowHover).Modulate(Color.White),
            E<ContainerButton>().Class(PdaHologramStyleClass.CopyRow).PseudoPressed().Box(copyRowHover).Modulate(Color.White),
            E<ContainerButton>().Class(PdaHologramStyleClass.CopyRow).PseudoDisabled().Box(copyRow).Modulate(Color.White),

            E<PdaHologramProgramTile>().Class(PdaHologramStyleClass.Tile).PseudoNormal().Box(tile).Modulate(Color.White),
            E<PdaHologramProgramTile>().Class(PdaHologramStyleClass.Tile).PseudoHovered().Box(tileHover).Modulate(Color.White),
            E<PdaHologramProgramTile>().Class(PdaHologramStyleClass.Tile).PseudoPressed().Box(tilePressed).Modulate(Color.White),
            E<PdaHologramProgramTile>().Class(PdaHologramStyleClass.Tile).PseudoDisabled().Box(tileDisabled).Modulate(Color.White),
        };

        if (!DeadSpaceStylePalette.Hologram)
            return rules.ToArray();

        // The projected window: palette frame and corner brackets instead of the coloured case. Its header is the
        // same glass band with a bright bottom line as on the other hologram windows and carries the top brackets.
        var shell = DeadSpaceStyleBoxes.Holo(
            DeadSpaceStylePalette.SurfaceDark,
            DeadSpaceStylePalette.BorderControl,
            bracket: WindowBracket);
        var header = DeadSpaceStyleBoxes.Holo(
            DeadSpaceStylePalette.SurfaceHeader,
            DeadSpaceStylePalette.BorderHeader,
            bracket: WindowBracket,
            brackets: HoloBrackets.Top,
            thickness: new Thickness(0, 0, 0, 1));
        var empty = new StyleBoxEmpty();

        rules.AddRange(
        [
            E<PanelContainer>()
                .Class("PdaBackgroundRect")
                .Class(PdaHologramStyleClass.Shell)
                .Panel(shell)
                .Modulate(Color.White),
            E<PanelContainer>()
                .Class("PdaBorderRect")
                .Class(PdaHologramStyleClass.Frame)
                .Panel(empty),
            E<PanelContainer>()
                .Class("PdaBackground")
                .Class(PdaHologramStyleClass.ScreenFrame)
                .Panel(empty)
                .Modulate(Color.White),
            E<PanelContainer>()
                .Class("PdaContentBackground")
                .Class(PdaHologramStyleClass.Screen)
                .Panel(empty)
                .Modulate(Color.White),
            E<PanelContainer>()
                .Class(PdaHologramStyleClass.Header)
                .Panel(header),
        ]);

        return rules.ToArray();
    }
}
