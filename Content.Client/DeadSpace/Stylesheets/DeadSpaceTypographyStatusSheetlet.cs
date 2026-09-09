// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Fonts;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.DeadSpace.Stylesheets;

[CommonSheetlet]
public sealed class DeadSpaceTypographyStatusSheetlet : Sheetlet<NanotrasenStylesheet>
{
    public override StyleRule[] GetRules(NanotrasenStylesheet sheet, object config)
    {
        var positiveBorder = DeadSpaceStylePalette.ClassicChrome
            ? DeadSpaceStylePalette.PositiveBorder
            : Color.Transparent;
        var negativeBorder = DeadSpaceStylePalette.ClassicChrome
            ? DeadSpaceStylePalette.NegativeBorder
            : Color.Transparent;
        var warningBorder = DeadSpaceStylePalette.ClassicChrome
            ? DeadSpaceStylePalette.WarningBorder
            : Color.Transparent;

        // Semantic color must never change a button's measured size. Keep the ordinary
        // semantic controls on the same 14x2 content margins as the global Button baseline.
        var accentControl = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.Control,
            Color.Transparent,
            new Thickness(1),
            14,
            2);
        var accentControlHover = new StyleBoxFlat(accentControl)
        {
            BackgroundColor = DeadSpaceStylePalette.ControlHover,
            BorderColor = DeadSpaceStylePalette.HoverOutline,
        };
        var accentControlPressed = new StyleBoxFlat(accentControl)
        {
            BackgroundColor = DeadSpaceStylePalette.ControlPressed,
            BorderColor = Color.Transparent,
        };
        var accentControlDisabled = new StyleBoxFlat(accentControl)
        {
            BackgroundColor = DeadSpaceStylePalette.ControlDisabled,
            BorderColor = Color.Transparent,
        };
        var positive = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.Positive,
            positiveBorder,
            new Thickness(1),
            14,
            2);
        var positiveHover = new StyleBoxFlat(positive)
        {
            BackgroundColor = DeadSpaceStylePalette.PositiveHover,
            BorderColor = DeadSpaceStylePalette.HoverOutline,
        };
        var positivePressed = new StyleBoxFlat(positive)
        {
            BackgroundColor = DeadSpaceStylePalette.PositivePressed,
            BorderColor = Color.Transparent,
        };
        var positiveDisabled = new StyleBoxFlat(positive)
        {
            BackgroundColor = DeadSpaceStylePalette.ControlDisabled,
            BorderColor = Color.Transparent,
        };
        var negative = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.Negative,
            negativeBorder,
            new Thickness(1),
            14,
            2);
        var negativeHover = new StyleBoxFlat(negative)
        {
            BackgroundColor = DeadSpaceStylePalette.NegativeHover,
            BorderColor = DeadSpaceStylePalette.HoverOutline,
        };
        var negativePressed = new StyleBoxFlat(negative)
        {
            BackgroundColor = DeadSpaceStylePalette.NegativeHover,
            BorderColor = Color.Transparent,
        };
        var negativeDisabled = new StyleBoxFlat(negative)
        {
            BackgroundColor = DeadSpaceStylePalette.ControlDisabled,
            BorderColor = Color.Transparent,
        };
        var warning = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.WarningControl,
            warningBorder,
            new Thickness(1),
            14,
            2);
        var warningHover = new StyleBoxFlat(warning)
        {
            BackgroundColor = DeadSpaceStylePalette.WarningControlHover,
            BorderColor = DeadSpaceStylePalette.HoverOutline,
        };
        var warningPressed = new StyleBoxFlat(warning)
        {
            BackgroundColor = DeadSpaceStylePalette.WarningControlPressed,
            BorderColor = Color.Transparent,
        };
        var warningDisabled = new StyleBoxFlat(warning)
        {
            BackgroundColor = DeadSpaceStylePalette.ControlDisabled,
            BorderColor = Color.Transparent,
        };

        // Large lobby actions retain their deliberate 14x8 geometry in every semantic state.
        var actionPositive = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.Positive,
            positiveBorder,
            new Thickness(1),
            14,
            8);
        var actionPositiveHover = new StyleBoxFlat(actionPositive)
        {
            BackgroundColor = DeadSpaceStylePalette.PositiveHover,
            BorderColor = DeadSpaceStylePalette.HoverOutline,
        };
        var actionPositivePressed = new StyleBoxFlat(actionPositive)
        {
            BackgroundColor = DeadSpaceStylePalette.PositivePressed,
            BorderColor = Color.Transparent,
        };
        var topActionNegative = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.Negative,
            negativeBorder,
            new Thickness(1),
            14,
            6);
        var topActionNegativeHover = new StyleBoxFlat(topActionNegative)
        {
            BackgroundColor = DeadSpaceStylePalette.NegativeHover,
            BorderColor = DeadSpaceStylePalette.HoverOutline,
        };
        var topActionNegativePressed = new StyleBoxFlat(topActionNegative)
        {
            BackgroundColor = DeadSpaceStylePalette.NegativeHover,
            BorderColor = Color.Transparent,
        };
        var topActionNegativeDisabled = new StyleBoxFlat(topActionNegative)
        {
            BackgroundColor = DeadSpaceStylePalette.ControlDisabled,
            BorderColor = Color.Transparent,
        };
        var readyOff = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.NegativeStrong,
            DeadSpaceStylePalette.ClassicChrome
                ? DeadSpaceStylePalette.NegativeBorderStrong
                : Color.Transparent,
            new Thickness(1),
            14,
            8);
        var readyOffHover = new StyleBoxFlat(readyOff)
        {
            BackgroundColor = DeadSpaceStylePalette.NegativeStrongHover,
            BorderColor = DeadSpaceStylePalette.HoverOutline,
        };
        var priorityNever = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.NegativeStrong,
            DeadSpaceStylePalette.ClassicChrome
                ? DeadSpaceStylePalette.NegativeBorderHover
                : Color.Transparent,
            new Thickness(1),
            14,
            2);
        var listUnread = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.Positive,
            positiveBorder,
            new Thickness(1),
            6,
            4);
        var listUnreadHover = new StyleBoxFlat(listUnread)
        {
            BackgroundColor = DeadSpaceStylePalette.PositiveHover,
            BorderColor = DeadSpaceStylePalette.HoverOutline,
        };
        var listUnreadDisabled = new StyleBoxFlat(listUnread)
        {
            BackgroundColor = DeadSpaceStylePalette.ControlDisabled,
            BorderColor = Color.Transparent,
        };
        var listPressed = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.ListItemPressed,
            Color.Transparent,
            new Thickness(1),
            6,
            4);
        // Destructive admin actions use the same strong red fill as the explicit "never" priority state.
        // The former generic negative fill made AHelp actions look like a separate, muddier palette.
        var danger = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.NegativeStrong,
            Color.Transparent,
            new Thickness(1),
            14,
            2);
        var dangerHover = new StyleBoxFlat(danger)
        {
            BackgroundColor = DeadSpaceStylePalette.NegativeStrongHover,
            BorderColor = DeadSpaceStylePalette.HoverOutline,
        };
        var dangerPressed = new StyleBoxFlat(danger)
        {
            BackgroundColor = DeadSpaceStylePalette.NegativeStrongHover,
            BorderColor = Color.Transparent,
        };
        var dangerDisabled = new StyleBoxFlat(danger)
        {
            BackgroundColor = DeadSpaceStylePalette.ControlDisabled,
            BorderColor = Color.Transparent,
        };
        var progressHighlight = DeadSpaceStyleBoxes.Flat(DeadSpaceStylePalette.Amber);
        progressHighlight.SetContentMarginOverride(StyleBox.Margin.Vertical, 14.5f);
        var progressAccent = DeadSpaceStyleBoxes.Flat(DeadSpaceStylePalette.Cyan);
        progressAccent.SetContentMarginOverride(StyleBox.Margin.Vertical, 14.5f);
        // RichTextLabel has no stylesheet font-color property and otherwise keeps its hardcoded light-grey
        // default. A neutral self-modulation makes both default and named bright markup colors readable on Light.
        var richTextModulate = DeadSpaceStylePalette.LightChrome
            ? Color.FromHex("#505860")
            : Color.White;
        var optionTriangleModulate = DeadSpaceStylePalette.LightChrome
            ? DeadSpaceStylePalette.TextInactive
            : Color.White;

        var rules = new List<StyleRule>
        {
            E<Label>()
                .Class(DeadSpaceStyleClass.Title)
                .Font(sheet.BaseFont.GetFont(16, FontKind.Bold))
                .FontColor(DeadSpaceStylePalette.Amber),
            E<Label>()
                .Class(DeadSpaceStyleClass.Subtitle)
                .Font(sheet.BaseFont.GetFont(10))
                .FontColor(DeadSpaceStylePalette.TextMuted),
            E<Label>()
                .Class(DeadSpaceStyleClass.SectionTitle)
                .Font(sheet.BaseFont.GetFont(12))
                .FontColor(DeadSpaceStylePalette.Amber),
            E<Label>()
                .Class(DeadSpaceStyleClass.ListHeader)
                .Font(sheet.BaseFont.GetFont(12, FontKind.Bold))
                .FontColor(DeadSpaceStylePalette.Amber),
            E<Label>()
                .Class(DeadSpaceStyleClass.RoundStatusTitle)
                .Font(sheet.BaseFont.GetFont(12, FontKind.Bold))
                .FontColor(DeadSpaceStylePalette.Amber),
            E<Label>()
                .Class(DeadSpaceStyleClass.RoundStatusTime)
                .Font(sheet.BaseFont.GetFont(16, FontKind.Bold))
                .FontColor(DeadSpaceStylePalette.Text),
            // RichTextLabel does not expose a stylesheet color property; keep its typography aligned here.
            E<RichTextLabel>()
                .Class(DeadSpaceStyleClass.Subtitle)
                .Font(sheet.BaseFont.GetFont(10)),
            E<RichTextLabel>()
                .Class(DeadSpaceStyleClass.SectionTitle)
                .Font(sheet.BaseFont.GetFont(12, FontKind.Bold)),
            E<RichTextLabel>()
                .Class(DeadSpaceStyleClass.Title)
                .Font(sheet.BaseFont.GetFont(16, FontKind.Bold)),
            E<ProgressBar>()
                .Class(DeadSpaceStyleClass.ProgressHighlight)
                .Prop(ProgressBar.StylePropertyForeground, progressHighlight),
            E<ProgressBar>()
                .Class(DeadSpaceStyleClass.ProgressAccent)
                .Prop(ProgressBar.StylePropertyForeground, progressAccent),

            Button(DeadSpaceStyleClass.Action).ParentOf(E<Label>()).Font(sheet.BaseFont.GetFont(12, FontKind.Bold)),
            Button(DeadSpaceStyleClass.ActionPositive).ParentOf(E<Label>()).Font(sheet.BaseFont.GetFont(12, FontKind.Bold)),
            Button(DeadSpaceStyleClass.Ready).ParentOf(E<Label>()).Font(sheet.BaseFont.GetFont(12, FontKind.Bold)),
            Button(DeadSpaceStyleClass.TopAction).ParentOf(E<Label>()).Font(sheet.BaseFont.GetFont(12, FontKind.Bold)),
            Button(DeadSpaceStyleClass.TopAction).ParentOf(E()).ParentOf(E<Label>()).Font(sheet.BaseFont.GetFont(12, FontKind.Bold)),
            Button(DeadSpaceStyleClass.TopAction).ParentOf(E<Label>().Class(OptionButton.StyleClassOptionButton)).Font(sheet.BaseFont.GetFont(12, FontKind.Bold)),
            Button(DeadSpaceStyleClass.ControlAccent).ParentOf(E<Label>()).Font(sheet.BaseFont.GetFont(12)),
            Button(DeadSpaceStyleClass.ControlDanger).ParentOf(E<Label>()).Font(sheet.BaseFont.GetFont(12)),
            Button(DeadSpaceStyleClass.ListItem).ParentOf(E<Label>()).Font(sheet.BaseFont.GetFont(12)),
            Button(DeadSpaceStyleClass.ListItemAlternate).ParentOf(E<Label>()).Font(sheet.BaseFont.GetFont(12)),

            Button(DeadSpaceStyleClass.ListItemUnread).ParentOf(E<Label>()).Font(sheet.BaseFont.GetFont(12)),
        };

        if (!DeadSpaceStylePalette.ClassicChrome)
        {
            // Dark and light use the shared minimalist DS14 baseline. Classic deliberately leaves ordinary
            // labels and semantic controls on the original Nanotrasen/Wizards rules.
            rules.AddRange(
            [
                E<Label>().FontColor(DeadSpaceStylePalette.Text),
                E<Label>().Class(StyleClass.LabelSubText).FontColor(DeadSpaceStylePalette.TextMuted),
                E<Label>().Class(StyleClass.LabelWeak).FontColor(DeadSpaceStylePalette.TextMuted),
                E<Label>().Class(StyleClass.LabelHeading).FontColor(DeadSpaceStylePalette.Amber),
                E<Label>().Class(StyleClass.LabelHeadingBigger).FontColor(DeadSpaceStylePalette.Amber),
                E<Label>().Class(StyleClass.LabelKeyText).FontColor(DeadSpaceStylePalette.Amber),
                E<Label>().Class(StyleClass.Positive).FontColor(DeadSpaceStylePalette.PositiveBorderPressed),
                E<Label>().Class(StyleClass.Negative).FontColor(DeadSpaceStylePalette.NegativeBorderHover),
                E<Label>().Class(StyleClass.Highlight).FontColor(DeadSpaceStylePalette.Amber),
                E<Label>().Class(StyleClass.StatusGood).FontColor(DeadSpaceStylePalette.PositiveBorderPressed),
                E<Label>().Class(StyleClass.StatusOkay).FontColor(DeadSpaceStylePalette.CyanBright),
                E<Label>().Class(StyleClass.StatusWarning).FontColor(DeadSpaceStylePalette.WarningBorderHover),
                E<Label>().Class(StyleClass.StatusBad).FontColor(DeadSpaceStylePalette.NegativeBorder),
                E<Label>().Class(StyleClass.StatusCritical).FontColor(DeadSpaceStylePalette.NegativeBorderHover),
                E<Label>().Class(DefaultWindow.StyleClassWindowTitle).FontColor(DeadSpaceStylePalette.Amber),
                E<Label>().Class("FancyWindowTitle").FontColor(DeadSpaceStylePalette.Amber),
                E<Label>().Class("WindowFooterText").FontColor(DeadSpaceStylePalette.TextMuted),
                E<RichTextLabel>().Modulate(richTextModulate),
                E<TextureRect>().Class(OptionButton.StyleClassOptionTriangle).Modulate(optionTriangleModulate),
                // ActionPositive is only assigned to Action buttons. Match the semantic class directly so it
                // remains authoritative even when the engine resolves equal-specificity class combinations.
                Button(DeadSpaceStyleClass.ActionPositive).PseudoNormal().Box(actionPositive).Modulate(Color.White),
                Button(DeadSpaceStyleClass.ActionPositive).PseudoHovered().Box(actionPositiveHover).Modulate(Color.White),
                Button(DeadSpaceStyleClass.ActionPositive).PseudoPressed().Box(actionPositivePressed).Modulate(Color.White),
                CompoundButton(DeadSpaceStyleClass.TopAction, StyleClass.Negative).PseudoNormal().Box(topActionNegative).Modulate(Color.White),
                CompoundButton(DeadSpaceStyleClass.TopAction, StyleClass.Negative).PseudoHovered().Box(topActionNegativeHover).Modulate(Color.White),
                CompoundButton(DeadSpaceStyleClass.TopAction, StyleClass.Negative).PseudoPressed().Box(topActionNegativePressed).Modulate(Color.White),
                CompoundButton(DeadSpaceStyleClass.TopAction, StyleClass.Negative).PseudoDisabled().Box(topActionNegativeDisabled).Modulate(Color.White),
                Button(StyleClass.Positive).PseudoNormal().Box(positive).Modulate(Color.White),
                Button(StyleClass.Positive).PseudoHovered().Box(positiveHover).Modulate(Color.White),
                Button(StyleClass.Positive).PseudoPressed().Box(positivePressed).Modulate(Color.White),
                Button(StyleClass.Positive).PseudoDisabled().Box(positiveDisabled).Modulate(Color.White),
                Button(StyleClass.Negative).PseudoNormal().Box(negative).Modulate(Color.White),
                Button(StyleClass.Negative).PseudoHovered().Box(negativeHover).Modulate(Color.White),
                Button(StyleClass.Negative).PseudoPressed().Box(negativePressed).Modulate(Color.White),
                Button(StyleClass.Negative).PseudoDisabled().Box(negativeDisabled).Modulate(Color.White),
                Button(DeadSpaceStyleClass.ControlAccent).PseudoNormal().Box(accentControl).Modulate(Color.White),
                Button(DeadSpaceStyleClass.ControlAccent).PseudoHovered().Box(accentControlHover).Modulate(Color.White),
                Button(DeadSpaceStyleClass.ControlAccent).PseudoPressed().Box(accentControlPressed).Modulate(Color.White),
                Button(DeadSpaceStyleClass.ControlAccent).PseudoDisabled().Box(accentControlDisabled).Modulate(Color.White),
                Button(DeadSpaceStyleClass.ControlPositive).PseudoNormal().Box(positive).Modulate(Color.White),
                Button(DeadSpaceStyleClass.ControlPositive).PseudoHovered().Box(positiveHover).Modulate(Color.White),
                Button(DeadSpaceStyleClass.ControlPositive).PseudoPressed().Box(positivePressed).Modulate(Color.White),
                Button(DeadSpaceStyleClass.ControlPositive).PseudoDisabled().Box(positiveDisabled).Modulate(Color.White),
                Button(DeadSpaceStyleClass.ControlWarning).PseudoNormal().Box(warning).Modulate(Color.White),
                Button(DeadSpaceStyleClass.ControlWarning).PseudoHovered().Box(warningHover).Modulate(Color.White),
                Button(DeadSpaceStyleClass.ControlWarning).PseudoPressed().Box(warningPressed).Modulate(Color.White),
                Button(DeadSpaceStyleClass.ControlWarning).PseudoDisabled().Box(warningDisabled).Modulate(Color.White),
                Button(DeadSpaceStyleClass.ControlDanger).PseudoNormal().Box(danger).Modulate(Color.White),
                Button(DeadSpaceStyleClass.ControlDanger).PseudoHovered().Box(dangerHover).Modulate(Color.White),
                Button(DeadSpaceStyleClass.ControlDanger).PseudoPressed().Box(dangerPressed).Modulate(Color.White),
                Button(DeadSpaceStyleClass.ControlDanger).PseudoDisabled().Box(dangerDisabled).Modulate(Color.White),
                Button(DeadSpaceStyleClass.Ready).PseudoNormal().Box(readyOff).Modulate(Color.White),
                Button(DeadSpaceStyleClass.Ready).PseudoHovered().Box(readyOffHover).Modulate(Color.White),
                Button(DeadSpaceStyleClass.Ready).PseudoPressed().Box(actionPositivePressed).Modulate(Color.White),
                Button(DeadSpaceStyleClass.JobPriorityPreferred).PseudoPressed().Box(positivePressed).Modulate(Color.White),
                Button(DeadSpaceStyleClass.JobPriorityNever).PseudoPressed().Box(priorityNever).Modulate(Color.White),
                Button(DeadSpaceStyleClass.AntagPreferenceOn).PseudoPressed().Box(positivePressed).Modulate(Color.White),
                Button(DeadSpaceStyleClass.AntagPreferenceOff).PseudoPressed().Box(priorityNever).Modulate(Color.White),
                Button(DeadSpaceStyleClass.ListItemUnread).PseudoNormal().Box(listUnread).Modulate(Color.White),
                Button(DeadSpaceStyleClass.ListItemUnread).PseudoHovered().Box(listUnreadHover).Modulate(Color.White),
                Button(DeadSpaceStyleClass.ListItemUnread).PseudoPressed().Box(listPressed).Modulate(Color.White),
                Button(DeadSpaceStyleClass.ListItemUnread).PseudoDisabled().Box(listUnreadDisabled).Modulate(Color.White),
            ]);
        }
        else
        {
            // Compatibility classes added by the redesign retain only their old semantic tint in Classic.
            // Their box texture and state geometry come from the original Nanotrasen/Wizards button sheetlet.
            rules.AddRange(
            [
                Button(DeadSpaceStyleClass.ActionPositive).PseudoNormal().Modulate(sheet.PositivePalette.Element),
                Button(DeadSpaceStyleClass.ActionPositive).PseudoHovered().Modulate(sheet.PositivePalette.HoveredElement),
                Button(DeadSpaceStyleClass.ActionPositive).PseudoPressed().Modulate(sheet.PositivePalette.PressedElement),
                Button(DeadSpaceStyleClass.ActionPositive).PseudoDisabled().Modulate(sheet.PositivePalette.DisabledElement),
                Button(DeadSpaceStyleClass.ControlPositive).PseudoNormal().Modulate(sheet.PositivePalette.Element),
                Button(DeadSpaceStyleClass.ControlPositive).PseudoHovered().Modulate(sheet.PositivePalette.HoveredElement),
                Button(DeadSpaceStyleClass.ControlPositive).PseudoPressed().Modulate(sheet.PositivePalette.PressedElement),
                Button(DeadSpaceStyleClass.ControlPositive).PseudoDisabled().Modulate(sheet.PositivePalette.DisabledElement),
                Button(DeadSpaceStyleClass.ControlWarning).PseudoNormal().Modulate(sheet.HighlightPalette.Element),
                Button(DeadSpaceStyleClass.ControlWarning).PseudoHovered().Modulate(sheet.HighlightPalette.HoveredElement),
                Button(DeadSpaceStyleClass.ControlWarning).PseudoPressed().Modulate(sheet.HighlightPalette.PressedElement),
                Button(DeadSpaceStyleClass.ControlWarning).PseudoDisabled().Modulate(sheet.HighlightPalette.DisabledElement),
                Button(DeadSpaceStyleClass.ControlDanger).PseudoNormal().Modulate(sheet.NegativePalette.Element),
                Button(DeadSpaceStyleClass.ControlDanger).PseudoHovered().Modulate(sheet.NegativePalette.HoveredElement),
                Button(DeadSpaceStyleClass.ControlDanger).PseudoPressed().Modulate(sheet.NegativePalette.PressedElement),
                Button(DeadSpaceStyleClass.ControlDanger).PseudoDisabled().Modulate(sheet.NegativePalette.DisabledElement),
                Button(DeadSpaceStyleClass.Ready).PseudoNormal().Modulate(sheet.NegativePalette.Element),
                Button(DeadSpaceStyleClass.Ready).PseudoHovered().Modulate(sheet.NegativePalette.HoveredElement),
                Button(DeadSpaceStyleClass.Ready).PseudoPressed().Modulate(sheet.PositivePalette.PressedElement),
                Button(DeadSpaceStyleClass.Ready).PseudoDisabled().Modulate(sheet.NegativePalette.DisabledElement),
                Button(DeadSpaceStyleClass.JobPriorityPreferred).PseudoPressed().Modulate(sheet.PositivePalette.PressedElement),
                Button(DeadSpaceStyleClass.JobPriorityNever).PseudoPressed().Modulate(sheet.NegativePalette.PressedElement),
                Button(DeadSpaceStyleClass.AntagPreferenceOn).PseudoPressed().Modulate(sheet.PositivePalette.PressedElement),
                Button(DeadSpaceStyleClass.AntagPreferenceOff).PseudoPressed().Modulate(sheet.NegativePalette.PressedElement),
                Button(DeadSpaceStyleClass.ListItemUnread).PseudoNormal().Modulate(sheet.PositivePalette.Element),
                Button(DeadSpaceStyleClass.ListItemUnread).PseudoHovered().Modulate(sheet.PositivePalette.HoveredElement),
                Button(DeadSpaceStyleClass.ListItemUnread).PseudoPressed().Modulate(sheet.PositivePalette.PressedElement),
                Button(DeadSpaceStyleClass.ListItemUnread).PseudoDisabled().Modulate(sheet.PositivePalette.DisabledElement),
            ]);
        }

        return rules.ToArray();
    }

    private static MutableSelectorElement Button(string styleClass)
    {
        return E<ContainerButton>().Class(styleClass);
    }

    private static MutableSelectorElement CompoundButton(string firstClass, string secondClass)
    {
        return Button(firstClass).Class(secondClass);
    }
}
