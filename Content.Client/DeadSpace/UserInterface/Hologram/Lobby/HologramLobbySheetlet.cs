// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.DeadSpace.Stylesheets;
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Lobby;

/// <summary>
/// "Dead Space" look of the lobby and the main menu: the shell dissolves into separate square projected panels with
/// corner brackets, headers with a 2px rule, a large pixel countdown and a "ready" action with a solid bright frame.
/// No glow anywhere. Other styles get no rules from here. Every rule pairs a lobby class with the DS14 class it
/// refines, so it outranks the general rule by one class.
/// </summary>
[CommonSheetlet]
public sealed class HologramLobbySheetlet : Sheetlet<NanotrasenStylesheet>
{
    private const float PanelBracket = 14f;

    /// <summary>Frame of the ready action, in UI units.</summary>
    private const float Frame = 2f;

    public override StyleRule[] GetRules(NanotrasenStylesheet sheet, object config)
    {
        if (!DeadSpaceStylePalette.Hologram)
            return [];

        var panel = DeadSpaceStyleBoxes.Holo(
            DeadSpaceStylePalette.Surface,
            DeadSpaceStylePalette.BorderControl,
            bracket: PanelBracket,
            horizontalMargin: 10,
            verticalMargin: 10);
        // A faint lit band with a 2px rule under it.
        var header = DeadSpaceStyleBoxes.Holo(
            DeadSpaceStylePalette.BorderHeader.WithAlpha(0.07f),
            DeadSpaceStylePalette.BorderControl,
            horizontalMargin: 12,
            verticalMargin: 7,
            thickness: new Thickness(0, 0, 0, 2));
        var status = DeadSpaceStyleBoxes.Holo(
            DeadSpaceStylePalette.SurfaceInset,
            DeadSpaceStylePalette.BorderDark,
            horizontalMargin: 18,
            verticalMargin: 8);
        var inset = DeadSpaceStyleBoxes.Holo(
            DeadSpaceStylePalette.SurfaceInset,
            DeadSpaceStylePalette.BorderControl,
            horizontalMargin: 6,
            verticalMargin: 6);

        var rules = new List<StyleRule>
        {
            E<PanelContainer>()
                .Class(DeadSpaceStyleClass.Window)
                .Class(HologramLobbyStyleClass.Shell)
                .Panel(new StyleBoxEmpty()),
            E<PanelContainer>()
                .Class(DeadSpaceStyleClass.Surface)
                .Class(HologramLobbyStyleClass.Panel)
                .Panel(panel),
            E<PanelContainer>()
                .Class(DeadSpaceStyleClass.SectionHeader)
                .Class(HologramLobbyStyleClass.Header)
                .Panel(header),
            E<PanelContainer>()
                .Class(DeadSpaceStyleClass.RoundStatus)
                .Class(HologramLobbyStyleClass.Status)
                .Panel(status),
            E<PanelContainer>()
                .Class(DeadSpaceStyleClass.Inset)
                .Class(HologramLobbyStyleClass.Inset)
                .Panel(inset),

            E<Label>()
                .Class(DeadSpaceStyleClass.Title)
                .Class(HologramLobbyStyleClass.Title)
                .Font(sheet.BaseFont.GetFont(14))
                .FontColor(DeadSpaceStylePalette.Heading),
            E<Label>()
                .Class(DeadSpaceStyleClass.RoundStatusTitle)
                .Class(HologramLobbyStyleClass.StatusTitle)
                .Font(sheet.BaseFont.GetFont(10))
                .FontColor(DeadSpaceStylePalette.TextMuted),
            E<Label>()
                .Class(DeadSpaceStyleClass.RoundStatusTime)
                .Class(HologramLobbyStyleClass.StatusTime)
                .Font(sheet.BaseFont.GetFont(24))
                .FontColor(DeadSpaceStylePalette.CyanBright),
            E<Label>()
                .Class(HologramLobbyStyleClass.CharacterName)
                .Font(sheet.BaseFont.GetFont(14))
                .FontColor(DeadSpaceStylePalette.CyanBright),
            E<Label>()
                .Class(HologramLobbyStyleClass.BigTitle)
                .Font(sheet.BaseFont.GetFont(32))
                .FontColor(DeadSpaceStylePalette.CyanBright),
            // The round line keeps its DS14 subtitle class; this one outranks it.
            E<Label>()
                .Class(DeadSpaceStyleClass.Subtitle)
                .Class(HologramLobbyStyleClass.Subtitle)
                .Font(sheet.BaseFont.GetFont(12))
                .FontColor(DeadSpaceStylePalette.TextMuted),
            E<Label>()
                .Class(HologramLobbyStyleClass.Caption)
                .Font(sheet.BaseFont.GetFont(10))
                .FontColor(DeadSpaceStylePalette.TextMuted),
            E<Label>()
                .Class(HologramLobbyStyleClass.Value)
                .Font(sheet.BaseFont.GetFont(12))
                .FontColor(DeadSpaceStylePalette.CyanBright),
        };

        AddReady(rules, sheet);
        return rules.ToArray();
    }

    /// <summary>
    /// The ready button is the main action of the lobby: a lit fill under a solid bright frame. Once the player is ready
    /// it turns green, so the state stays readable at a glance.
    /// </summary>
    private static void AddReady(List<StyleRule> rules, NanotrasenStylesheet sheet)
    {
        var lit = DeadSpaceStylePalette.BorderHeader;
        AddStates(
            rules,
            Ready,
            Box(lit.WithAlpha(0.20f), DeadSpaceStylePalette.BorderHeader),
            Box(lit.WithAlpha(0.28f), DeadSpaceStylePalette.PressedOutline),
            Box(DeadSpaceStylePalette.PositivePressed, DeadSpaceStylePalette.PositiveBorder),
            Box(DeadSpaceStylePalette.ActionDisabled, DeadSpaceStylePalette.BorderDisabled));

        rules.AddRange(
        [
            Ready()
                .ParentOf(E<Label>())
                .Font(sheet.BaseFont.GetFont(16)),
            Ready()
                .PseudoPressed()
                .ParentOf(E<Label>())
                .FontColor(DeadSpaceStylePalette.PositiveBorderPressed),
        ]);
    }

    private static MutableSelectorElement Ready()
    {
        return E<ContainerButton>().Class(DeadSpaceStyleClass.Action).Class(DeadSpaceStyleClass.Ready);
    }

    private static DeadSpaceHoloStyleBox Box(Color background, Color border)
    {
        // The geometry of a lobby action (14x8), square, with a 2px frame.
        return DeadSpaceStyleBoxes.Holo(
            background,
            border,
            horizontalMargin: 14,
            verticalMargin: 8,
            thickness: new Thickness(Frame));
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
