// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.UserInterface.Systems.Chat.Controls;
using Robust.Client.UserInterface;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Chat;

/// <summary>
/// Switches the HUD chat between its stock layout and the projected one: header strip on top, the transcript set in
/// from the frame, the composer as an inset field.
/// </summary>
public static class HologramChatLayout
{
    /// <summary>Marks the composer that sits inside the projected frame as an inset field.</summary>
    public const string StyleClassComposer = "DeadSpaceChatComposer";

    // Stock margins from ChatBox.xaml.
    private static readonly Thickness StockTranscriptMargin = new(8, 8, 8, 4);
    private static readonly Thickness StockComposerMargin = new(2);

    private static readonly Thickness TranscriptMargin = new(16, 8, 14, 2);
    private static readonly Thickness ComposerMargin = new(14, 4, 14, 14);

    public static void Apply(bool framed, HologramChatHeader header, Control transcript, ChatInputBox composer)
    {
        header.Visible = framed;
        transcript.Margin = framed ? TranscriptMargin : StockTranscriptMargin;
        composer.Margin = framed ? ComposerMargin : StockComposerMargin;

        if (framed)
            composer.AddStyleClass(StyleClassComposer);
        else
            composer.RemoveStyleClass(StyleClassComposer);
    }
}
