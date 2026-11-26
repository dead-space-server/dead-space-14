// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.ContextMenu.UI;
using Content.Client.DeadSpace.Stylesheets;
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Stylesheets;
using Content.Client.Verbs.UI;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Comms;

/// <summary>
/// "Dead Space" style of the communications console's primary actions and of the entity context menu: a dark
/// projected list with thin dividers, the hovered row lit with a bright bar on its left edge.
/// </summary>
[CommonSheetlet]
public sealed class HologramCommsSheetlet : Sheetlet<NanotrasenStylesheet>
{
    /// <summary>The main action of a console section ("announce", "broadcast"): a lit button with a bright frame.</summary>
    public const string StyleClassPrimary = "DeadSpaceCommsPrimary";

    /// <summary>Width of the bar that marks the hovered context menu row.</summary>
    private const float RowBar = 3f;

    public override StyleRule[] GetRules(NanotrasenStylesheet sheet, object config)
    {
        if (!DeadSpaceStylePalette.Hologram)
            return [];

        var rules = new List<StyleRule>();

        AddButtonRules(rules, () => E<ContainerButton>().Class(StyleClassPrimary),
            Primary(DeadSpaceStylePalette.Cyan.WithAlpha(0.2f), DeadSpaceStylePalette.BorderHeader),
            Primary(DeadSpaceStylePalette.Cyan.WithAlpha(0.32f), DeadSpaceStylePalette.CyanBright),
            Primary(DeadSpaceStylePalette.ControlPressed, DeadSpaceStylePalette.PressedOutline),
            Primary(DeadSpaceStylePalette.ControlDisabled, DeadSpaceStylePalette.BorderDisabled));

        rules.Add(E<PanelContainer>()
            .Class(ContextMenuPopup.StyleClassContextMenuPopup)
            .Panel(DeadSpaceStyleBoxes.Holo(
                DeadSpaceStylePalette.SurfacePopup,
                DeadSpaceStylePalette.BorderControl,
                verticalMargin: 4)));

        var divider = DeadSpaceStylePalette.BorderDark;
        AddButtonRules(rules, () => E<ContextMenuElement>().Class(ContextMenuElement.StyleClassContextMenuButton),
            Row(Color.Transparent, divider, hovered: false),
            Row(DeadSpaceStylePalette.Cyan.WithAlpha(0.16f), DeadSpaceStylePalette.BorderHeader, hovered: true),
            Row(DeadSpaceStylePalette.Cyan.WithAlpha(0.28f), DeadSpaceStylePalette.PressedOutline, hovered: true),
            Row(Color.Transparent, divider, hovered: false));

        AddButtonRules(rules,
            () => E<ContextMenuElement>().Class(ConfirmationMenuElement.StyleClassConfirmationContextMenuButton),
            Row(DeadSpaceStylePalette.Negative, DeadSpaceStylePalette.NegativeBorder, hovered: false),
            Row(DeadSpaceStylePalette.NegativeHover, DeadSpaceStylePalette.NegativeBorderStrong, hovered: true),
            Row(DeadSpaceStylePalette.NegativeStrong, DeadSpaceStylePalette.NegativeBorderHover, hovered: true),
            Row(DeadSpaceStylePalette.Negative, DeadSpaceStylePalette.NegativeBorder, hovered: false));

        return rules.ToArray();
    }

    private static StyleBox Primary(Color background, Color border)
    {
        return DeadSpaceStyleBoxes.Holo(background, border, horizontalMargin: 14, verticalMargin: 4);
    }

    /// <summary>
    /// A context menu row: a divider under a resting row, a bar on the left of a lit one. The content margins don't
    /// change between the two, so the text never shifts on hover.
    /// </summary>
    private static StyleBox Row(Color background, Color border, bool hovered)
    {
        var box = DeadSpaceStyleBoxes.Holo(
            background,
            border,
            thickness: hovered ? new Thickness(RowBar, 0, 0, 0) : new Thickness(0, 0, 0, 1));
        box.ContentMarginLeftOverride = RowBar;
        box.ContentMarginTopOverride = 0;
        box.ContentMarginRightOverride = 0;
        box.ContentMarginBottomOverride = 1;
        return box;
    }

    /// <summary>
    /// Boxes per button state. The stock context menu tints its rows through modulate, so it is reset to white.
    /// </summary>
    private static void AddButtonRules(
        List<StyleRule> rules,
        Func<MutableSelectorElement> selector,
        StyleBox normal,
        StyleBox hovered,
        StyleBox pressed,
        StyleBox disabled)
    {
        rules.AddRange(
        [
            selector().PseudoNormal().Box(normal).Modulate(Color.White),
            selector().PseudoHovered().Box(hovered).Modulate(Color.White),
            selector().PseudoPressed().Box(pressed).Modulate(Color.White),
            selector().PseudoDisabled().Box(disabled).Modulate(Color.White),
        ]);
    }
}
