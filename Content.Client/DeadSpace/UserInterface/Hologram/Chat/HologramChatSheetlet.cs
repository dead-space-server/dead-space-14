// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.DeadSpace.Stylesheets;
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Fonts;
using Content.Client.Stylesheets.Stylesheets;
using Content.Client.UserInterface.Systems.Chat.Controls;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Chat;

/// <summary>
/// Details of the projected HUD chat in the "Dead Space" style: the header strip, its close control and the inset
/// composer. The frame itself is drawn by <see cref="HologramChatPanel"/>, which also owns the chat opacity.
/// </summary>
[CommonSheetlet]
public sealed class HologramChatSheetlet : Sheetlet<NanotrasenStylesheet>
{
    public override StyleRule[] GetRules(NanotrasenStylesheet sheet, object config)
    {
        if (!DeadSpaceStylePalette.Hologram)
            return [];

        // A projected strip over the top of the frame. It covers the frame's top edge and brackets, so it draws them.
        var header = DeadSpaceStyleBoxes.Holo(
            DeadSpaceStylePalette.Cyan.WithAlpha(0.07f),
            DeadSpaceStylePalette.BorderControl,
            bracket: HologramChatPanel.Bracket,
            brackets: HoloBrackets.Top);
        header.ContentMarginLeftOverride = 16;
        header.ContentMarginRightOverride = 8;
        header.ContentMarginTopOverride = 5;
        header.ContentMarginBottomOverride = 5;

        var close = DeadSpaceStyleBoxes.Holo(
            Color.Transparent,
            DeadSpaceStylePalette.BorderControl,
            chamfer: 0);
        var closeHover = DeadSpaceStyleBoxes.Holo(
            DeadSpaceStylePalette.ControlHover,
            DeadSpaceStylePalette.HoverOutline,
            chamfer: 0);
        var closePressed = DeadSpaceStyleBoxes.Holo(
            DeadSpaceStylePalette.ControlPressed,
            DeadSpaceStylePalette.PressedOutline,
            chamfer: 0);

        // The composer reads as a field set into the panel rather than a second bracketed panel.
        var composer = DeadSpaceStyleBoxes.Holo(
            DeadSpaceStylePalette.SurfaceInset,
            DeadSpaceStylePalette.BorderInset,
            horizontalMargin: 4,
            verticalMargin: 2);

        return
        [
            E<PanelContainer>()
                .Class(HologramChatHeader.StyleClassHeader)
                .Panel(header),
            E<Label>()
                .Class(HologramChatHeader.StyleClassTitle)
                .Font(sheet.BaseFont.GetFont(12, FontKind.Bold))
                .FontColor(DeadSpaceStylePalette.Heading),
            E<Label>()
                .Class(HologramChatHeader.StyleClassChannels)
                .Font(sheet.BaseFont.GetFont(10))
                .FontColor(DeadSpaceStylePalette.TextMuted),

            E<ContainerButton>().Class(HologramChatHeader.StyleClassClose).PseudoNormal()
                .Box(close).Modulate(Color.White),
            E<ContainerButton>().Class(HologramChatHeader.StyleClassClose).PseudoHovered()
                .Box(closeHover).Modulate(Color.White),
            E<ContainerButton>().Class(HologramChatHeader.StyleClassClose).PseudoPressed()
                .Box(closePressed).Modulate(Color.White),
            E<ContainerButton>().Class(HologramChatHeader.StyleClassClose).PseudoDisabled()
                .Box(close).Modulate(Color.White),
            E<TextureRect>()
                .Class(HologramChatHeader.StyleClassCloseIcon)
                .Modulate(DeadSpaceStylePalette.Cyan),
            E<ContainerButton>().Class(HologramChatHeader.StyleClassClose).PseudoHovered()
                .ParentOf(E<TextureRect>().Class(HologramChatHeader.StyleClassCloseIcon))
                .Modulate(DeadSpaceStylePalette.CyanBright),

            // Two classes outrank the bracketed ChatPanel box the base chat composer gets.
            E<PanelContainer>()
                .Class(ChatInputBox.StyleClassChatPanel)
                .Class(HologramChatLayout.StyleClassComposer)
                .Panel(composer),
        ];
    }
}
