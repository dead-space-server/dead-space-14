// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Fonts;
using Content.Client.Stylesheets.Stylesheets;
using Content.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.DeadSpace.Stylesheets;

[CommonSheetlet]
public sealed class DeadSpaceSurfaceSheetlet : Sheetlet<NanotrasenStylesheet>
{
    public override StyleRule[] GetRules(NanotrasenStylesheet sheet, object config)
    {
        StyleBox shell;
        StyleBox topShell;
        if (DeadSpaceStylePalette.ClassicChrome)
        {
            var shellTexture = ResCache.GetTexture("/Textures/Interface/Nano/lobby_b.png");
            var classicShell = new StyleBoxTexture
            {
                Texture = shellTexture,
                Mode = StyleBoxTexture.StretchMode.Tile,
            };
            classicShell.SetPatchMargin(StyleBox.Margin.All, 24);
            classicShell.SetExpandMargin(StyleBox.Margin.All, -4);
            classicShell.SetContentMarginOverride(StyleBox.Margin.All, 10);
            shell = classicShell;

            var classicTopShell = new StyleBoxTexture(classicShell);
            classicTopShell.SetContentMarginOverride(StyleBox.Margin.Horizontal, 10);
            classicTopShell.SetContentMarginOverride(StyleBox.Margin.Vertical, 7);
            topShell = classicTopShell;
        }
        else
        {
            shell = DeadSpaceStyleBoxes.Flat(
                DeadSpaceStylePalette.SurfaceDark,
                horizontalMargin: 10,
                verticalMargin: 10);
            topShell = DeadSpaceStyleBoxes.Flat(
                DeadSpaceStylePalette.SurfaceDark,
                horizontalMargin: 10,
                verticalMargin: 7);
        }

        // Pre-redesign Wizards panels used tonal separation for ordinary surfaces; only genuine insets
        // carried a persistent edge.
        var classicBorder = Color.Transparent;
        var classicInsetBorder = DeadSpaceStylePalette.ClassicChrome
            ? DeadSpaceStylePalette.BorderInset
            : Color.Transparent;
        var classicThickness = new Thickness(0);
        var classicInsetThickness = DeadSpaceStylePalette.ClassicChrome
            ? new Thickness(2)
            : new Thickness(0);

        var panel = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.Surface,
            classicBorder,
            classicThickness,
            horizontalMargin: 10,
            verticalMargin: 10);
        var panelDark = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.SurfaceDark,
            DeadSpaceStylePalette.ClassicChrome ? DeadSpaceStylePalette.BorderDark : Color.Transparent,
            classicThickness,
            horizontalMargin: 8,
            verticalMargin: 8);
        var panelWarning = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.Warning);
        var panelPositive = DeadSpaceStyleBoxes.Flat(DeadSpaceStylePalette.Positive);
        var panelNegative = DeadSpaceStyleBoxes.Flat(DeadSpaceStylePalette.Negative);
        var panelHighlight = DeadSpaceStyleBoxes.Flat(DeadSpaceStylePalette.WarningControl);
        var modalScrim = DeadSpaceStyleBoxes.Flat(DeadSpaceStylePalette.ModalScrim);
        var flatBody = DeadSpaceStyleBoxes.Flat(DeadSpaceStylePalette.SurfaceFlat, horizontalMargin: 8);
        flatBody.ContentMarginTopOverride = 8;
        flatBody.ContentMarginBottomOverride = 6;

        var header = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.SurfaceHeader,
            DeadSpaceStylePalette.AccentDim,
            new Thickness(0, 0, 0, 1),
            10,
            8);
        var inset = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.SurfaceInset,
            classicInsetBorder,
            classicInsetThickness,
            horizontalMargin: 6,
            verticalMargin: 6);
        var roundStatus = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.SurfaceStatus,
            Color.Transparent,
            classicThickness,
            horizontalMargin: 18,
            verticalMargin: 7);

        var accent = DeadSpaceStyleBoxes.Flat(DeadSpaceStylePalette.Amber);
        accent.ContentMarginLeftOverride = 2;
        accent.ContentMarginBottomOverride = 2;
        var accentDim = DeadSpaceStyleBoxes.Flat(DeadSpaceStylePalette.AccentDim);
        accentDim.ContentMarginLeftOverride = 1;
        accentDim.ContentMarginBottomOverride = 1;
        var lowDivider = DeadSpaceStyleBoxes.Flat(DeadSpaceStylePalette.AccentDim);

        var characterIcon = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.SurfaceIcon,
            DeadSpaceStylePalette.ClassicChrome ? DeadSpaceStylePalette.BorderIcon : Color.Transparent,
            new Thickness(1),
            3,
            3);
        var defaultTabsPanel = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.SurfaceTabs,
            classicBorder,
            classicThickness);
        var tabThickness = DeadSpaceStylePalette.ClassicChrome
            ? new Thickness(1)
            : new Thickness(0, 0, 0, 2);
        var defaultTabActive = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.SurfaceTabActive,
            DeadSpaceStylePalette.BorderTabActive,
            tabThickness,
            7,
            4);
        var defaultTabInactive = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.SurfaceTabInactive,
            DeadSpaceStylePalette.BorderTabInactive,
            tabThickness,
            7,
            4);

        var listHeader = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.SurfaceHeader,
            DeadSpaceStylePalette.BorderHeader,
            new Thickness(0, 0, 0, 1),
            7,
            5);
        var listItem = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.ListItem,
            Color.Transparent,
            DeadSpaceStylePalette.ClassicChrome ? new Thickness(0) : new Thickness(1),
            6,
            4);
        var listItemAlternate = new StyleBoxFlat(listItem)
        {
            BackgroundColor = DeadSpaceStylePalette.ListItemAlternate,
        };
        var input = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.Input,
            DeadSpaceStylePalette.ClassicChrome
                ? DeadSpaceStylePalette.BorderControl
                : Color.Transparent,
            DeadSpaceStylePalette.ClassicChrome ? new Thickness(0) : new Thickness(1),
            7,
            4);
        var popup = new StyleBoxFlat(panel)
        {
            BackgroundColor = DeadSpaceStylePalette.SurfacePopup,
            BorderColor = DeadSpaceStylePalette.HoverOutline,
        };
        var optionBackground = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.SurfacePopup,
            DeadSpaceStylePalette.BorderDark,
            new Thickness(1));
        // Preserve the zero-margin geometry of widespread legacy panel classes while changing only their chrome.
        var legacyPanelDeep = DeadSpaceStyleBoxes.Flat(DeadSpaceStylePalette.SurfaceDark);
        var legacyPanelInsetDeep = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.SurfaceInset,
            classicInsetBorder,
            new Thickness(2));
        // Modern windows use tonal separation instead of a permanent dark frame.
        var defaultWindowPanel = DeadSpaceStyleBoxes.Flat(DeadSpaceStylePalette.Surface);
        var defaultWindowHeader = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.SurfaceHeader,
            DeadSpaceStylePalette.AccentDim,
            new Thickness(0, 0, 0, 1));
        // FancyWindow content needs one raised neutral step: deep black is reserved for genuine insets/HUD.
        var backgroundPanel = DeadSpaceStyleBoxes.Flat(DeadSpaceStylePalette.SurfaceFlat);

        var rules = new List<StyleRule>
        {
            E<PanelContainer>().Class(DeadSpaceStyleClass.Window).Panel(shell),
            E<PanelContainer>().Class(DeadSpaceStyleClass.WindowTop).Panel(topShell),
            E<PanelContainer>().Class(DeadSpaceStyleClass.Surface).Panel(panel),
            E<PanelContainer>().Class(DeadSpaceStyleClass.SurfaceDark).Panel(panelDark),
            E<PanelContainer>().Class(DeadSpaceStyleClass.SurfaceWarning).Panel(panelWarning),
            E<PanelContainer>().Class(DeadSpaceStyleClass.ModalScrim).Panel(modalScrim),
            E<PanelContainer>().Class(DeadSpaceStyleClass.SurfaceFlat).Panel(flatBody),
            E<PanelContainer>().Class(DeadSpaceStyleClass.SectionHeader).Panel(header),
            E<PanelContainer>().Class(DeadSpaceStyleClass.Inset).Panel(inset),
            E<PanelContainer>().Class(DeadSpaceStyleClass.RoundStatus).Panel(roundStatus),
            E<PanelContainer>().Class(DeadSpaceStyleClass.Accent).Panel(accent),
            E<PanelContainer>().Class(DeadSpaceStyleClass.AccentDim).Panel(accentDim),
            E<PanelContainer>().Class(DeadSpaceStyleClass.CharacterIcon).Panel(characterIcon),
            E<PanelContainer>().Class(DeadSpaceStyleClass.ListHeader).Panel(listHeader),
            E<PanelContainer>().Class(DeadSpaceStyleClass.ListItem).Panel(listItem),
            E<PanelContainer>().Class(DeadSpaceStyleClass.ListItemAlternate).Panel(listItemAlternate),
            E<PanelContainer>().Class(DeadSpaceStyleClass.TextField).Panel(input),
            E<PanelContainer>().Class(DeadSpaceStyleClass.Popup).Panel(popup),
        };

        // The classic option deliberately keeps the original Nanotrasen/Wizards rules for controls
        // without an explicit DS14 style class. Only bridge the classes used by the fixed layouts above.
        if (DeadSpaceStylePalette.ClassicChrome)
            return rules.ToArray();

        rules.AddRange(
        [
            // The legacy low divider appears across content UI; keep it quiet and one logical pixel thick.
            E<PanelContainer>()
                .Class(StyleClass.LowDivider)
                .Panel(lowDivider)
                .MinSize(new Vector2(1, 1)),
            E<PanelContainer>().Class(StyleClass.PanelDeep).Panel(legacyPanelDeep),
            E<PanelContainer>().Class(StyleClass.PanelInsetDeep).Panel(legacyPanelInsetDeep),
            E<PanelContainer>().Class("BackgroundDark").Panel(legacyPanelDeep),
            E<PanelContainer>().Class(StyleClass.Positive).Panel(panelPositive),
            E<PanelContainer>().Class(StyleClass.Negative).Panel(panelNegative),
            E<PanelContainer>().Class(StyleClass.Highlight).Panel(panelHighlight),
            // Standard content windows use DS chrome without per-window style annotations.
            E<PanelContainer>().Class(DefaultWindow.StyleClassWindowPanel).Panel(defaultWindowPanel),
            E<PanelContainer>().Class(DefaultWindow.StyleClassWindowHeader).Panel(defaultWindowHeader),
            // The legacy PanelSheetlet also applies a dark color through Modulate. Reset it here or the
            // DS14 flat color is multiplied twice and FancyWindow bodies become almost pure black.
            E<PanelContainer>()
                .Class(StyleClass.BackgroundPanel)
                .Panel(backgroundPanel)
                .Modulate(Color.White),
            E<PanelContainer>().Class("WindowHeadingBackground").Panel(defaultWindowHeader),
            E<PanelContainer>().Class(OptionButton.StyleClassOptionsBackground).Panel(optionBackground),
            E<TabContainer>()
                .Prop(TabContainer.StylePropertyPanelStyleBox, defaultTabsPanel)
                .Prop(TabContainer.StylePropertyTabStyleBox, defaultTabActive)
                .Prop(TabContainer.StylePropertyTabStyleBoxInactive, defaultTabInactive)
                .Prop(TabContainer.stylePropertyTabFontColor, DeadSpaceStylePalette.Text)
                .Prop(TabContainer.StylePropertyTabFontColorInactive, DeadSpaceStylePalette.TextInactive)
                .Prop("font", sheet.BaseFont.GetFont(12)),
        ]);

        return rules.ToArray();
    }
}
