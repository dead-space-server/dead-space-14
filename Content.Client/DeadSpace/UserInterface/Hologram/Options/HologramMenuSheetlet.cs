// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.DeadSpace.Stylesheets;
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Stylesheets;
using Content.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Options;

/// <summary>
/// "Dead Space" look of the escape menu and the settings window: square outlined menu rows with a 2px frame, red
/// destructive actions, a main action with a solid bright frame, an underlined tab strip and slim flat sliders. No glow
/// anywhere. Other styles get no rules from here, so the classes of <see cref="HologramMenuStyleClass"/> change nothing
/// there. Every rule outranks the general button rules by one class, so the result doesn't depend on sheetlet order.
/// </summary>
[CommonSheetlet]
public sealed class HologramMenuSheetlet : Sheetlet<NanotrasenStylesheet>
{
    /// <summary>Frame of menu rows and highlighted buttons, in UI units.</summary>
    private const float Frame = 2f;

    public override StyleRule[] GetRules(NanotrasenStylesheet sheet, object config)
    {
        if (!DeadSpaceStylePalette.Hologram)
            return [];

        var rules = new List<StyleRule>();
        AddMenuRows(rules, sheet);
        AddPrimary(rules);
        // After the rows: a red row is still a row, but red.
        AddDanger(rules);
        AddTabs(rules, sheet);
        AddSliders(rules, sheet);
        AddSettingsRows(rules);
        return rules.ToArray();
    }

    private static void AddMenuRows(List<StyleRule> rules, NanotrasenStylesheet sheet)
    {
        AddStates(
            rules,
            () => Btn(HologramMenuStyleClass.MenuRow),
            Box(DeadSpaceStylePalette.Action, DeadSpaceStylePalette.BorderControl, 16, 8),
            Box(DeadSpaceStylePalette.ActionHover, DeadSpaceStylePalette.BorderHeader, 16, 8),
            Box(DeadSpaceStylePalette.ActionPressed, DeadSpaceStylePalette.PressedOutline, 16, 8),
            Box(DeadSpaceStylePalette.ActionDisabled, DeadSpaceStylePalette.BorderDisabled, 16, 8));

        // A row that asks for attention (new changelog entries) is lit instead of turning red: red is for danger.
        AddStates(
            rules,
            () => E<ContainerButton>().Class(HologramMenuStyleClass.MenuRow).Class(StyleClass.Negative),
            Box(Lit(0.20f), DeadSpaceStylePalette.BorderHeader, 16, 8),
            Box(Lit(0.28f), DeadSpaceStylePalette.PressedOutline, 16, 8),
            Box(DeadSpaceStylePalette.ActionPressed, DeadSpaceStylePalette.PressedOutline, 16, 8),
            Box(DeadSpaceStylePalette.ActionDisabled, DeadSpaceStylePalette.BorderDisabled, 16, 8));

        rules.AddRange(
        [
            Btn(HologramMenuStyleClass.MenuRow)
                .ParentOf(E<Label>())
                .Font(sheet.BaseFont.GetFont(13)),
            // The general rule centres every button caption through the same style property.
            Btn(HologramMenuStyleClass.MenuRow)
                .ParentOf(E<Label>().Class(ContainerButton.StyleClassButton))
                .AlignMode(Label.AlignMode.Left),
        ]);
    }

    private static void AddPrimary(List<StyleRule> rules)
    {
        // Selection in this style is a solid bright frame over a lit fill.
        AddStates(
            rules,
            () => Btn(HologramMenuStyleClass.Primary),
            Box(Lit(0.20f), DeadSpaceStylePalette.BorderHeader, 14, 2),
            Box(Lit(0.28f), DeadSpaceStylePalette.PressedOutline, 14, 2),
            Box(DeadSpaceStylePalette.ActionPressed, DeadSpaceStylePalette.PressedOutline, 14, 2),
            Box(DeadSpaceStylePalette.ActionDisabled, DeadSpaceStylePalette.BorderDisabled, 14, 2));
    }

    private static void AddDanger(List<StyleRule> rules)
    {
        // Each kind of button keeps its own margins; only frame, fill and caption turn red.
        AddDangerStates(rules, () => Btn(HologramMenuStyleClass.Danger), 14, 2);
        AddDangerStates(
            rules,
            () => E<ContainerButton>().Class(DeadSpaceStyleClass.Action).Class(HologramMenuStyleClass.Danger),
            14,
            8);
        AddDangerStates(
            rules,
            () => E<ContainerButton>().Class(HologramMenuStyleClass.MenuRow).Class(HologramMenuStyleClass.Danger),
            16,
            8);

        rules.AddRange(
        [
            Btn(HologramMenuStyleClass.Danger)
                .ParentOf(E<Label>())
                .FontColor(DeadSpaceStylePalette.NegativeBorderStrong),
            Btn(HologramMenuStyleClass.Danger)
                .PseudoHovered()
                .ParentOf(E<Label>())
                .FontColor(DeadSpaceStylePalette.NegativeBorderHover),
            Btn(HologramMenuStyleClass.Danger)
                .PseudoDisabled()
                .ParentOf(E<Label>())
                .FontColor(DeadSpaceStylePalette.TextDisabled),
        ]);
    }

    private static void AddDangerStates(
        List<StyleRule> rules,
        Func<MutableSelectorElement> selector,
        float horizontalMargin,
        float verticalMargin)
    {
        AddStates(
            rules,
            selector,
            Box(DeadSpaceStylePalette.Negative, DeadSpaceStylePalette.NegativeBorder, horizontalMargin, verticalMargin),
            Box(DeadSpaceStylePalette.NegativeHover, DeadSpaceStylePalette.NegativeBorderStrong, horizontalMargin, verticalMargin),
            Box(DeadSpaceStylePalette.NegativeStrong, DeadSpaceStylePalette.NegativeBorderHover, horizontalMargin, verticalMargin),
            Box(DeadSpaceStylePalette.ActionDisabled, DeadSpaceStylePalette.BorderDisabled, horizontalMargin, verticalMargin));
    }

    private static void AddTabs(List<StyleRule> rules, NanotrasenStylesheet sheet)
    {
        // The strip sits on the window itself; a thin line under it separates the page.
        var panel = new StyleBoxFlat
        {
            BackgroundColor = Color.Transparent,
            BorderColor = DeadSpaceStylePalette.BorderControl,
            BorderThickness = new Thickness(0, 1, 0, 0),
        };
        panel.ContentMarginTopOverride = 10;

        var active = new StyleBoxFlat
        {
            BackgroundColor = Lit(0.18f),
            BorderColor = DeadSpaceStylePalette.BorderHeader,
            BorderThickness = new Thickness(0, 0, 0, 2),
        };
        active.SetContentMarginOverride(StyleBox.Margin.Horizontal, 14);
        active.ContentMarginTopOverride = 7;
        active.ContentMarginBottomOverride = 6;

        // Same geometry, so switching tabs doesn't shift the strip.
        var inactive = new StyleBoxFlat(active)
        {
            BackgroundColor = Color.Transparent,
            BorderColor = Color.Transparent,
        };

        rules.Add(E<TabContainer>()
            .Class(HologramMenuStyleClass.Tabs)
            .Prop(TabContainer.StylePropertyPanelStyleBox, panel)
            .Prop(TabContainer.StylePropertyTabStyleBox, active)
            .Prop(TabContainer.StylePropertyTabStyleBoxInactive, inactive)
            .Prop(TabContainer.stylePropertyTabFontColor, DeadSpaceStylePalette.Text)
            .Prop(TabContainer.StylePropertyTabFontColorInactive, DeadSpaceStylePalette.TextMuted)
            .Prop("font", sheet.BaseFont.GetFont(11)));
    }

    private static void AddSliders(List<StyleRule> rules, NanotrasenStylesheet sheet)
    {
        // The slider keeps its full height for the grabber and the mouse; only a thin flat band in the middle is drawn.
        const float band = 8f;

        var track = new HologramBandStyleBox
        {
            BackgroundColor = Lit(0.10f),
            BandHeight = band,
        };
        var outline = new HologramBandStyleBox
        {
            BorderColor = Lit(0.20f),
            BandHeight = band,
        };
        var fill = new HologramBandStyleBox
        {
            BackgroundColor = DeadSpaceStylePalette.BorderHeader,
            BandHeight = band,
        };
        var grabber = new HologramBandStyleBox
        {
            BackgroundColor = DeadSpaceStylePalette.CyanBright,
            BandWidth = 3,
            BandHeight = 14,
        };
        grabber.SetContentMarginOverride(StyleBox.Margin.Horizontal, 4);
        grabber.SetContentMarginOverride(StyleBox.Margin.Vertical, 8);

        rules.AddRange(
        [
            E<Slider>()
                .Class(HologramMenuStyleClass.Slider)
                .Prop(Slider.StylePropertyBackground, track)
                .Prop(Slider.StylePropertyForeground, outline)
                .Prop(Slider.StylePropertyFill, fill)
                .Prop(Slider.StylePropertyGrabber, grabber),
            E<Label>()
                .Class(HologramMenuStyleClass.SliderValue)
                .Font(sheet.BaseFont.GetFont(12))
                .FontColor(DeadSpaceStylePalette.CyanBright),
        ]);
    }

    private static void AddSettingsRows(List<StyleRule> rules)
    {
        rules.AddRange(
        [
            // Label on the left, the control takes the rest of the row, like the sliders do.
            E<OptionButton>()
                .Class(HologramMenuStyleClass.DropDown)
                .HorizontalExpand(true),
            // No stripe behind the reset/apply buttons; its top edge stays as the divider.
            E<StripeBack>()
                .Class(HologramMenuStyleClass.Footer)
                .Prop(StripeBack.StylePropertyBackground, new StyleBoxEmpty()),
        ]);
    }

    /// <summary>The projection colour as a translucent fill.</summary>
    private static Color Lit(float alpha)
    {
        return DeadSpaceStylePalette.BorderHeader.WithAlpha(alpha);
    }

    /// <summary>A square button box with a 2px frame.</summary>
    private static DeadSpaceHoloStyleBox Box(
        Color background,
        Color border,
        float horizontalMargin,
        float verticalMargin)
    {
        return DeadSpaceStyleBoxes.Holo(
            background,
            border,
            horizontalMargin: horizontalMargin,
            verticalMargin: verticalMargin,
            thickness: new Thickness(Frame));
    }

    private static MutableSelectorElement Btn(string styleClass)
    {
        return E<ContainerButton>().Class(ContainerButton.StyleClassButton).Class(styleClass);
    }

    /// <summary>Selectors are mutable builders, so every state rule starts from a fresh one.</summary>
    private static void AddStates(
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
