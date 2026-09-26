using Content.Client.DeadSpace.Stylesheets;
using Content.Client.PDA;
using Content.Client.Stylesheets;
// DS14-start
using Content.Client.Stylesheets.Sheetlets;
using Content.Client.Stylesheets.SheetletConfigs;
// DS14-end
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.PDA;

[CommonSheetlet]
public sealed class PdaSheetlet : Sheetlet<NanotrasenStylesheet>
{
    public override StyleRule[] GetRules(NanotrasenStylesheet sheet, object config)
    {
        // DS14-start
        IPanelConfig panelConfig = sheet;
        StyleBox contentBackground;
        StyleBox accentBackground;
        StyleBox shellBackground;
        StyleBox borderRect;
        Color contentModulate;
        Color accentModulate;
        Color shellModulate;
        Color normalBorder;
        Color disabledBorder;
        Color settingsNormalColor;
        Color settingsHoverColor;
        Color settingsPressedColor;
        Color settingsDisabledColor;
        Color programNormalColor;
        Color programHoverColor;
        Color programPressedColor;
        Color programDisabledColor;
        Color positiveLabelColor;
        Color contentFooterColor;
        Color windowFooterColor;
        Thickness controlBorderThickness;
        if (DeadSpaceStylePalette.ClassicChrome)
        {
            // Restore the PDA from before the DS14 UI intervention (7074b42): stock Nanotrasen textures,
            // neutral grey rows and the original green hover/pressed state.
            contentBackground = StyleBoxHelpers.SquareStyleBox(sheet);
            accentBackground = StyleBoxHelpers.SquareStyleBox(sheet);
            shellBackground = StyleBoxHelpers.BaseStyleBox(sheet);
            borderRect = sheet.GetTexture(panelConfig.GeometricPanelBorderPath)
                .IntoPatch(StyleBox.Margin.All, 10);
            contentModulate = Color.FromHex("#25252A");
            accentModulate = Color.Black;
            shellModulate = Color.FromHex("#717059");
            normalBorder = Color.Transparent;
            disabledBorder = Color.Transparent;
            settingsNormalColor = Color.FromHex("#313138");
            settingsHoverColor = Color.FromHex("#3E6C45");
            settingsPressedColor = Color.FromHex("#3E6C45");
            settingsDisabledColor = Color.FromHex("#313138");
            programNormalColor = Color.FromHex("#313138");
            programHoverColor = Color.FromHex("#3E6C45");
            programPressedColor = Color.FromHex("#3E6C45");
            programDisabledColor = Color.FromHex("#313138");
            positiveLabelColor = Color.White;
            contentFooterColor = Color.FromHex("#757575");
            windowFooterColor = Color.FromHex("#333D3B");
            controlBorderThickness = new Thickness(0);
        }
        else
        {
            contentBackground = new StyleBoxFlat
            {
                BackgroundColor = DeadSpaceStylePalette.SurfaceInset,
            };
            // These panels are tinted at runtime from PdaBorderColorComponent. Keep their source color white so
            // the prototype color is preserved instead of being multiplied by an already-dark DS14 surface.
            accentBackground = DeadSpaceStyleBoxes.Flat(Color.White);
            shellBackground = DeadSpaceStyleBoxes.Flat(Color.White);
            borderRect = DeadSpaceStyleBoxes.Flat(
                Color.Transparent,
                DeadSpaceStylePalette.BorderDark,
                new Thickness(1));
            contentModulate = Color.White;
            accentModulate = DeadSpaceStylePalette.SurfaceIcon;
            shellModulate = DeadSpaceStylePalette.SurfaceStatus;
            normalBorder = Color.Transparent;
            disabledBorder = Color.Transparent;
            settingsNormalColor = DeadSpaceStylePalette.Control;
            settingsHoverColor = DeadSpaceStylePalette.ControlHover;
            settingsPressedColor = DeadSpaceStylePalette.ControlPressed;
            settingsDisabledColor = DeadSpaceStylePalette.ControlDisabled;
            programNormalColor = DeadSpaceStylePalette.ListItem;
            programHoverColor = DeadSpaceStylePalette.ListItemHover;
            programPressedColor = DeadSpaceStylePalette.ListItemPressed;
            programDisabledColor = DeadSpaceStylePalette.ControlDisabled;
            positiveLabelColor = DeadSpaceStylePalette.PositiveBorderPressed;
            contentFooterColor = DeadSpaceStylePalette.TextMuted;
            windowFooterColor = DeadSpaceStylePalette.TextMuted;
            controlBorderThickness = new Thickness(1);
        }

        var settingsNormal = DeadSpaceStyleBoxes.Flat(
            settingsNormalColor,
            normalBorder,
            controlBorderThickness,
            9,
            4);
        var settingsHover = new StyleBoxFlat(settingsNormal)
        {
            BackgroundColor = settingsHoverColor,
            BorderColor = DeadSpaceStylePalette.ClassicChrome ? Color.Transparent : DeadSpaceStylePalette.HoverOutline,
        };
        var settingsPressed = new StyleBoxFlat(settingsNormal)
        {
            BackgroundColor = settingsPressedColor,
            BorderColor = Color.Transparent,
        };
        var settingsDisabled = new StyleBoxFlat(settingsNormal)
        {
            BackgroundColor = settingsDisabledColor,
            BorderColor = disabledBorder,
        };
        var settingsPositive = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.Positive,
            Color.Transparent,
            controlBorderThickness,
            9,
            4);
        var settingsPositiveHover = new StyleBoxFlat(settingsPositive)
        {
            BackgroundColor = DeadSpaceStylePalette.PositiveHover,
            BorderColor = DeadSpaceStylePalette.ClassicChrome ? Color.Transparent : DeadSpaceStylePalette.HoverOutline,
        };
        var settingsPositivePressed = new StyleBoxFlat(settingsPositive)
        {
            BackgroundColor = DeadSpaceStylePalette.PositivePressed,
            BorderColor = Color.Transparent,
        };

        var programNormal = DeadSpaceStyleBoxes.Flat(
            programNormalColor,
            normalBorder,
            controlBorderThickness,
            6,
            4);
        var programHover = new StyleBoxFlat(programNormal)
        {
            BackgroundColor = programHoverColor,
            BorderColor = DeadSpaceStylePalette.ClassicChrome ? Color.Transparent : DeadSpaceStylePalette.HoverOutline,
        };
        var programPressed = new StyleBoxFlat(programNormal)
        {
            BackgroundColor = programPressedColor,
            BorderColor = Color.Transparent,
        };
        var programDisabled = new StyleBoxFlat(programNormal)
        {
            BackgroundColor = programDisabledColor,
            BorderColor = disabledBorder,
        };
        var homeRow = DeadSpaceStyleBoxes.Flat(
            Color.Transparent,
            horizontalMargin: 6,
            verticalMargin: 3);
        // DS14-end

        return
        [
            //PDA - Backgrounds
            E<PanelContainer>()
                .Class("PdaContentBackground")
                // DS14-start
                .Prop(PanelContainer.StylePropertyPanel, contentBackground)
                .Prop(Control.StylePropertyModulateSelf, contentModulate),
                // DS14-end

            E<PanelContainer>()
                .Class("PdaBackground")
                // DS14-start
                .Prop(PanelContainer.StylePropertyPanel, accentBackground)
                .Prop(Control.StylePropertyModulateSelf, accentModulate),
                // DS14-end

            E<PanelContainer>()
                .Class("PdaBackgroundRect")
                // DS14-start
                .Prop(PanelContainer.StylePropertyPanel, shellBackground)
                .Prop(Control.StylePropertyModulateSelf, shellModulate),
                // DS14-end

            E<PanelContainer>()
                .Class("PdaBorderRect")
                .Prop(PanelContainer.StylePropertyPanel, borderRect), // DS14

            //PDA - Buttons
            // DS14-start
            E<PdaSettingsButton>()
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Box(settingsNormal),

            E<PdaSettingsButton>()
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Box(settingsHover),

            E<PdaSettingsButton>()
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .Box(settingsPressed),

            E<PdaSettingsButton>()
                .Pseudo(ContainerButton.StylePseudoClassDisabled)
                .Box(settingsDisabled),

            E<PdaSettingsButton>()
                .Class(DeadSpaceStyleClass.ControlPositive)
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Box(settingsPositive),

            E<PdaSettingsButton>()
                .Class(DeadSpaceStyleClass.ControlPositive)
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Box(settingsPositiveHover),

            E<PdaSettingsButton>()
                .Class(DeadSpaceStyleClass.ControlPositive)
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .Box(settingsPositivePressed),

            E<PdaSettingsButton>()
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .ParentOf(E())
                .ParentOf(E<Label>())
                .Prop(Label.StylePropertyFontColor, DeadSpaceStylePalette.Text),

            E<PdaSettingsButton>()
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .ParentOf(E())
                .ParentOf(E<Label>())
                .Prop(Label.StylePropertyFontColor, DeadSpaceStylePalette.Text),

            E<PdaSettingsButton>()
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .ParentOf(E())
                .ParentOf(E<Label>())
                .Prop(Label.StylePropertyFontColor, DeadSpaceStylePalette.Text),

            E<PdaSettingsButton>()
                .Pseudo(ContainerButton.StylePseudoClassDisabled)
                .ParentOf(E())
                .ParentOf(E<Label>())
                .Prop(Label.StylePropertyFontColor, DeadSpaceStylePalette.TextPlaceholder),

            E<PdaSettingsButton>()
                .Class(DeadSpaceStyleClass.ControlPositive)
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .ParentOf(E())
                .ParentOf(E<Label>())
                .Prop(Label.StylePropertyFontColor, positiveLabelColor),

            E<PdaSettingsButton>()
                .Class(DeadSpaceStyleClass.ControlPositive)
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .ParentOf(E())
                .ParentOf(E<Label>())
                .Prop(Label.StylePropertyFontColor, positiveLabelColor),

            E<PdaSettingsButton>()
                .Class(DeadSpaceStyleClass.ControlPositive)
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .ParentOf(E())
                .ParentOf(E<Label>())
                .Prop(Label.StylePropertyFontColor, positiveLabelColor),

            E<PdaProgramItem>()
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Box(programNormal),

            E<PdaProgramItem>()
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Box(programHover),

            E<PdaProgramItem>()
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .Box(programPressed),

            E<PdaProgramItem>()
                .Pseudo(ContainerButton.StylePseudoClassDisabled)
                .Box(programDisabled),

            E<BoxContainer>()
                .Class("PdaHomeSummary")
                .ParentOf(E<ContainerButton>().Pseudo(ContainerButton.StylePseudoClassNormal))
                .Box(homeRow),

            E<BoxContainer>()
                .Class("PdaHomeSummary")
                .ParentOf(E<ContainerButton>().Pseudo(ContainerButton.StylePseudoClassHover))
                .Box(homeRow),

            E<BoxContainer>()
                .Class("PdaHomeSummary")
                .ParentOf(E<ContainerButton>().Pseudo(ContainerButton.StylePseudoClassPressed))
                .Box(homeRow),

            E<BoxContainer>()
                .Class("PdaHomeSummary")
                .ParentOf(E<ContainerButton>().Pseudo(ContainerButton.StylePseudoClassDisabled))
                .Box(homeRow),
            // DS14-end

            //PDA - Text
            E<Label>()
                .Class("PdaContentFooterText")
                .Prop(Label.StylePropertyFont, sheet.BaseFont.GetFont(10))
                .Prop(Label.StylePropertyFontColor, contentFooterColor), // DS14

            E<Label>()
                .Class("PdaWindowFooterText")
                .Prop(Label.StylePropertyFont, sheet.BaseFont.GetFont(10))
                .Prop(Label.StylePropertyFontColor, windowFooterColor), // DS14
        ];
    }
}
