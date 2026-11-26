// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.DeadSpace.Stylesheets;
using Content.Shared.DeadSpace.CCCCVars;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Chat;

/// <summary>
/// Background of the HUD chat. It keeps the player's chat opacity (<c>chat.window_opacity</c>) and rebuilds the
/// background from it whenever the interface style or the projection colour changes. In the "Dead Space" style the
/// chat becomes a projected panel: thin frame and bright corner brackets; its position and size stay as they are.
/// </summary>
public sealed class HologramChatPanel : PanelContainer
{
    /// <summary>Same geometry as the hologram windows (square corners), so the chat reads as one of them.</summary>
    public const float Bracket = 14f;
    public const float Chamfer = 0f;

    /// <summary>Alpha of the "Dead Space" transcript surface at the stock window opacity.</summary>
    private const float StockTranscriptAlpha = 224f / 255f;

    private float? _opacity;
    private IConfigurationManager? _cfg;

    /// <summary>
    /// True while the projected frame is drawn: the "Dead Space" style is active and the chat sits on the HUD (it has
    /// an opacity). The header strip and the inset composer follow it.
    /// </summary>
    public bool Framed { get; private set; }

    /// <summary>Raised when <see cref="Framed"/> changes.</summary>
    public event Action<bool>? FramedChanged;

    /// <summary>Sets the chat window opacity and redraws the background with it.</summary>
    public void SetOpacity(float opacity)
    {
        _opacity = Math.Clamp(opacity, 0f, 1f);
        UpdatePanel();
    }

    protected override void StylePropertiesChanged()
    {
        base.StylePropertiesChanged();
        UpdatePanel();
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _cfg ??= IoCManager.Resolve<IConfigurationManager>();
        _cfg.OnValueChanged(CCCCVars.HologramChatFrame, OnChatFrameChanged);
        UpdatePanel();
    }

    protected override void ExitedTree()
    {
        _cfg?.UnsubValueChanged(CCCCVars.HologramChatFrame, OnChatFrameChanged);
        base.ExitedTree();
    }

    private void OnChatFrameChanged(bool _)
    {
        UpdatePanel();
    }

    /// <summary>The player may turn the projected chat frame off and keep a plain backing.</summary>
    private bool FrameEnabled => (_cfg ??= IoCManager.Resolve<IConfigurationManager>()).GetCVar(CCCCVars.HologramChatFrame);

    private void UpdatePanel()
    {
        var framed = false;

        // A chat without an opacity (pop-out window, lobby, previews) keeps whatever background it was given.
        if (_opacity is { } opacity)
        {
            if (DeadSpaceStylePalette.Hologram && FrameEnabled)
            {
                framed = true;
                PanelOverride = CreateFrame(opacity);
            }
            else
            {
                Color color;
                if (TryGetStyleProperty<StyleBox>(StylePropertyPanel, out var style) && style is StyleBoxFlat flat)
                    color = flat.BackgroundColor;
                else
                    color = DeadSpaceStylePalette.SurfaceTranscript.WithAlpha(221f / 255f);

                PanelOverride = new StyleBoxFlat
                {
                    BackgroundColor = color.WithAlpha(opacity),
                };
            }

            InvalidateMeasure();
        }

        if (framed == Framed)
            return;

        Framed = framed;
        FramedChanged?.Invoke(framed);
    }

    private static DeadSpaceHoloStyleBox CreateFrame(float opacity)
    {
        // The chat opacity slider keeps its meaning; the window opacity of the projection scales it further.
        var surface = DeadSpaceStylePalette.SurfaceTranscript;
        var alpha = Math.Clamp(opacity * surface.A / StockTranscriptAlpha, 0f, 1f);

        var box = DeadSpaceStyleBoxes.Holo(
            surface.WithAlpha(alpha),
            DeadSpaceStylePalette.BorderControl,
            bracket: Bracket,
            chamfer: Chamfer);

        // The header strip lies flush with the frame and draws the top edge and brackets itself; the transcript and
        // the composer bring their own margins.
        box.ContentMarginLeftOverride = 0;
        box.ContentMarginTopOverride = 0;
        box.ContentMarginRightOverride = 0;
        box.ContentMarginBottomOverride = 0;
        return box;
    }
}
