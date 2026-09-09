// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Chat.Controls;
using Content.Client.UserInterface.Systems.Chat.Widgets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.DeadSpace.Stylesheets;

[CommonSheetlet]
public sealed class DeadSpaceControlSheetlet : Sheetlet<NanotrasenStylesheet>
{
    public override StyleRule[] GetRules(NanotrasenStylesheet sheet, object config)
    {
        // Dark and light separate controls from their parent through fill, not permanent frames. Retaining the
        // one-pixel transparent edge keeps existing layout geometry stable and leaves the gold edge for hover.
        var normalBorder = DeadSpaceStylePalette.ClassicChrome
            ? DeadSpaceStylePalette.BorderControl
            : Color.Transparent;
        var disabledBorder = DeadSpaceStylePalette.ClassicChrome
            ? DeadSpaceStylePalette.BorderDisabled
            : Color.Transparent;

        var action = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.Action,
            normalBorder,
            new Thickness(1),
            14,
            8);
        var actionHover = new StyleBoxFlat(action)
        {
            BackgroundColor = DeadSpaceStylePalette.ActionHover,
            BorderColor = DeadSpaceStylePalette.HoverOutline,
        };
        var actionPressed = new StyleBoxFlat(action)
        {
            BackgroundColor = DeadSpaceStylePalette.ActionPressed,
            BorderColor = normalBorder,
        };
        var actionDisabled = new StyleBoxFlat(action)
        {
            BackgroundColor = DeadSpaceStylePalette.ActionDisabled,
            BorderColor = disabledBorder,
        };

        var topAction = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.Control,
            normalBorder,
            new Thickness(1),
            14,
            6);
        var topActionHover = new StyleBoxFlat(topAction)
        {
            BackgroundColor = DeadSpaceStylePalette.ActionHover,
            BorderColor = DeadSpaceStylePalette.HoverOutline,
        };
        var topActionPressed = new StyleBoxFlat(topAction)
        {
            BackgroundColor = DeadSpaceStylePalette.ActionPressed,
            BorderColor = normalBorder,
        };
        var topActionDisabled = new StyleBoxFlat(topAction)
        {
            BackgroundColor = DeadSpaceStylePalette.ControlDisabled,
            BorderColor = disabledBorder,
        };

        var dangerControl = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.Control,
            normalBorder,
            new Thickness(1),
            14,
            2);
        var dangerControlHover = new StyleBoxFlat(dangerControl)
        {
            BackgroundColor = DeadSpaceStylePalette.ControlHover,
            BorderColor = DeadSpaceStylePalette.HoverOutline,
        };
        var dangerControlPressed = new StyleBoxFlat(dangerControl)
        {
            BackgroundColor = DeadSpaceStylePalette.ControlPressed,
            BorderColor = normalBorder,
        };
        var dangerControlDisabled = new StyleBoxFlat(dangerControl)
        {
            BackgroundColor = DeadSpaceStylePalette.ControlDisabled,
            BorderColor = disabledBorder,
        };
        // Match the existing base button content margins so a global chrome change cannot resize layouts.
        var baseControl = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.Control,
            normalBorder,
            new Thickness(1),
            14,
            2);
        var baseControlHover = new StyleBoxFlat(baseControl)
        {
            BackgroundColor = DeadSpaceStylePalette.ControlHover,
            BorderColor = DeadSpaceStylePalette.HoverOutline,
        };
        var baseControlPressed = new StyleBoxFlat(baseControl)
        {
            BackgroundColor = DeadSpaceStylePalette.ControlPressed,
            BorderColor = normalBorder,
        };
        var baseControlDisabled = new StyleBoxFlat(baseControl)
        {
            BackgroundColor = DeadSpaceStylePalette.ControlDisabled,
            BorderColor = disabledBorder,
        };
        // Bare ContainerButton is widely used as a zero-margin interactive row. Give it safe global chrome
        // without changing its measured geometry; the more specific ordinary Button rule below keeps its margins.
        var bareContainer = DeadSpaceStyleBoxes.Flat(DeadSpaceStylePalette.Control);
        var bareContainerHover = DeadSpaceStyleBoxes.Flat(DeadSpaceStylePalette.ControlHover);
        var bareContainerPressed = DeadSpaceStyleBoxes.Flat(DeadSpaceStylePalette.ControlPressed);
        var bareContainerDisabled = DeadSpaceStyleBoxes.Flat(DeadSpaceStylePalette.ControlDisabled);

        var listItem = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.ListItem,
            normalBorder,
            new Thickness(1),
            6,
            4);
        var listItemAlternate = new StyleBoxFlat(listItem)
        {
            BackgroundColor = DeadSpaceStylePalette.ListItemAlternate,
        };
        var listItemHover = new StyleBoxFlat(listItem)
        {
            BackgroundColor = DeadSpaceStylePalette.ListItemHover,
            BorderColor = DeadSpaceStylePalette.HoverOutline,
        };
        var listItemPressed = new StyleBoxFlat(listItem)
        {
            BackgroundColor = DeadSpaceStylePalette.ListItemPressed,
            BorderColor = normalBorder,
        };
        var listItemDisabled = new StyleBoxFlat(listItem)
        {
            BackgroundColor = DeadSpaceStylePalette.ControlDisabled,
            BorderColor = disabledBorder,
        };
        // ListContainerButton historically had a zero-margin StyleBoxOverride. Preserve that geometry.
        var listContainer = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.ListItem,
            normalBorder,
            DeadSpaceStylePalette.ClassicChrome ? new Thickness(1) : new Thickness(0));
        var listContainerHover = DeadSpaceStyleBoxes.Flat(DeadSpaceStylePalette.ListItemHover);
        var listContainerPressed = DeadSpaceStyleBoxes.Flat(DeadSpaceStylePalette.ListItemPressed);
        var listContainerDisabled = DeadSpaceStyleBoxes.Flat(DeadSpaceStylePalette.ControlDisabled);

        var input = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.Input,
            normalBorder,
            new Thickness(1),
            7,
            4);
        var inputDisabled = new StyleBoxFlat(input)
        {
            BackgroundColor = DeadSpaceStylePalette.ControlDisabled,
            BorderColor = disabledBorder,
        };
        // OutputPanel content commonly contains channel/name markup authored for a dark transcript. Keep this
        // surface dark in both themes instead of applying a destructive tint to arbitrary markup colors.
        var textArea = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.SurfaceTranscript,
            normalBorder,
            new Thickness(1),
            7,
            6);
        var chatPanel = DeadSpaceStyleBoxes.Flat(
            DeadSpaceStylePalette.SurfaceTranscript.WithAlpha(221f / 255f));

        var rules = new List<StyleRule>
        {
            // ChatWindowPanel owns the complete transcript background and its configurable opacity.
            // Target the transcript directly so ResizableChatBox and pop-out variants behave identically.
            E<OutputPanel>()
                .Class(ChatBox.StyleClassChatTranscript)
                .Prop(OutputPanel.StylePropertyStyleBox, new StyleBoxEmpty()),
            // Keep the HUD chat composer comfortably tappable/readable without increasing every LineEdit.
            // The surrounding ChatPanel remains responsible for its background, avoiding another nested box.
            E<LineEdit>()
                .Class(ChatInputBox.StyleClassChatLineEdit)
                .MinSize(new Vector2(0, 34)),
        };

        if (!DeadSpaceStylePalette.ClassicChrome)
        {
            rules.AddRange(
            [
                E<ItemList>()
                    .Prop(ItemList.StylePropertyBackground, input)
                    .Prop(ItemList.StylePropertyItemBackground, listItem)
                    .Prop(ItemList.StylePropertyDisabledItemBackground, inputDisabled)
                    .Prop(ItemList.StylePropertySelectedItemBackground, listItemPressed)
                    .Prop("font-color", DeadSpaceStylePalette.Text),
                E<OutputPanel>()
                    .Prop(OutputPanel.StylePropertyStyleBox, textArea),
                E<LineEdit>()
                    .Prop(LineEdit.StylePropertyStyleBox, input)
                    .Prop("font-color", DeadSpaceStylePalette.Text)
                    .Prop(LineEdit.StylePropertyCursorColor, DeadSpaceStylePalette.Amber)
                    .Prop(LineEdit.StylePropertySelectionColor, DeadSpaceStylePalette.CyanSelection),
                E<LineEdit>()
                    .Class(LineEdit.StyleClassLineEditNotEditable)
                    .Prop(LineEdit.StylePropertyStyleBox, inputDisabled)
                    .Prop("font-color", DeadSpaceStylePalette.TextMuted),
                E<LineEdit>()
                    .Pseudo(LineEdit.StylePseudoClassPlaceholder)
                    .Prop("font-color", DeadSpaceStylePalette.TextPlaceholder),
                E<TextEdit>()
                    .Prop("font-color", DeadSpaceStylePalette.Text)
                    .Prop(TextEdit.StylePropertyCursorColor, DeadSpaceStylePalette.Amber)
                    .Prop(TextEdit.StylePropertySelectionColor, DeadSpaceStylePalette.CyanSelection),
                E<TextEdit>()
                    .Pseudo(TextEdit.StylePseudoClassPlaceholder)
                    .Prop("font-color", DeadSpaceStylePalette.TextPlaceholder),
                E<PanelContainer>()
                    .Class(ChatInputBox.StyleClassChatPanel)
                    .Prop(PanelContainer.StylePropertyPanel, chatPanel),
                E<LineEdit>()
                    .Class(ChatInputBox.StyleClassChatLineEdit)
                    .Prop(LineEdit.StylePropertyStyleBox, new StyleBoxEmpty())
                    .Prop("font-color", DeadSpaceStylePalette.TextOnTranscript),
                E<LineEdit>()
                    .Class(ChatInputBox.StyleClassChatLineEdit)
                    .Pseudo(LineEdit.StylePseudoClassPlaceholder)
                    .Prop("font-color", DeadSpaceStylePalette.TextOnTranscriptPlaceholder),
            ]);

            AddBareContainerButtonRules(
                rules,
                bareContainer,
                bareContainerHover,
                bareContainerPressed,
                bareContainerDisabled);
            AddCheckBoxRules(rules);
            // SwitchButton already communicates every state through its track and thumb.
            // Keep its ContainerButton root free of the global full-row backing surface.
            var switchBackground = new StyleBoxEmpty();
            rules.AddRange(
            [
                E<SwitchButton>().PseudoNormal().Box(switchBackground).Modulate(Color.White),
                E<SwitchButton>().PseudoHovered().Box(switchBackground).Modulate(Color.White),
                E<SwitchButton>().PseudoPressed().Box(switchBackground).Modulate(Color.White),
                E<SwitchButton>().PseudoDisabled().Box(switchBackground).Modulate(Color.White),
            ]);
            AddButtonRules(rules, null, baseControl, baseControlHover, baseControlPressed, baseControlDisabled);
        }

        if (!DeadSpaceStylePalette.ClassicChrome)
        {
            AddButtonRules(rules, DeadSpaceStyleClass.Action, action, actionHover, actionPressed, actionDisabled);
            AddButtonRules(rules, DeadSpaceStyleClass.TopAction, topAction, topActionHover, topActionPressed, topActionDisabled);
            AddButtonRules(
                rules,
                DeadSpaceStyleClass.ControlDanger,
                dangerControl,
                dangerControlHover,
                dangerControlPressed,
                dangerControlDisabled);
            AddButtonRules(rules, DeadSpaceStyleClass.ListItem, listItem, listItemHover, listItemPressed, listItemDisabled);
            AddButtonRules(rules, DeadSpaceStyleClass.ListItemAlternate, listItemAlternate, listItemHover, listItemPressed, listItemDisabled);
            AddButtonRules(rules,
                ListContainer.StyleClassListContainerButton,
                listContainer,
                listContainerHover,
                listContainerPressed,
                listContainerDisabled);
        }

        return rules.ToArray();
    }

    private static void AddButtonRules(
        List<StyleRule> rules,
        string? styleClass,
        StyleBox normal,
        StyleBox hovered,
        StyleBox pressed,
        StyleBox disabled)
    {
        rules.AddRange(
        [
            Button(styleClass).PseudoNormal().Box(normal).Modulate(Color.White),
            Button(styleClass).PseudoHovered().Box(hovered).Modulate(Color.White),
            Button(styleClass).PseudoPressed().Box(pressed).Modulate(Color.White),
            Button(styleClass).PseudoDisabled().Box(disabled).Modulate(Color.White),
        ]);
    }

    private static MutableSelectorElement Button(string? styleClass)
    {
        var selector = E<ContainerButton>();
        return styleClass == null
            ? selector.Class(ContainerButton.StyleClassButton)
            : selector.Class(styleClass);
    }

    private static void AddBareContainerButtonRules(
        List<StyleRule> rules,
        StyleBox normal,
        StyleBox hovered,
        StyleBox pressed,
        StyleBox disabled)
    {
        rules.AddRange(
        [
            E<ContainerButton>().PseudoNormal().Box(normal).Modulate(Color.White),
            E<ContainerButton>().PseudoHovered().Box(hovered).Modulate(Color.White),
            E<ContainerButton>().PseudoPressed().Box(pressed).Modulate(Color.White),
            E<ContainerButton>().PseudoDisabled().Box(disabled).Modulate(Color.White),
        ]);
    }

    private static void AddCheckBoxRules(List<StyleRule> rules)
    {
        // The checkbox texture already communicates true/false and hover. A full-row style box reads as text
        // selection in dense settings lists, so keep the row completely transparent in every state.
        var background = new StyleBoxEmpty();
        rules.AddRange(
        [
            E<CheckBox>().PseudoNormal().Box(background).Modulate(Color.White),
            E<CheckBox>().PseudoHovered().Box(background).Modulate(Color.White),
            E<CheckBox>().PseudoPressed().Box(background).Modulate(Color.White),
            E<CheckBox>().PseudoDisabled().Box(background).Modulate(Color.White),
        ]);
    }
}
