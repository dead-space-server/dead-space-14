// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Body.Limbs;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Input;

namespace Content.Client.DeadSpace.Body.Limbs;

public sealed class LimbDollView : Control
{
    public const float Scale = 2.25f;
    public const float AlertScale = 2f;

    public LimbZone? Selected;
    public event Action<LimbZone?>? OnZone;

    private RSI? _sheet;

    public LimbDollView()
    {
        MouseFilter = MouseFilterMode.Stop;
        MinSize = Plate;
        SetSize = Plate;
        HorizontalExpand = false;
        VerticalExpand = false;
        OnKeyBindDown += OnClick;
    }

    private static Vector2 Plate => new Vector2(LimbDollLayout.Width, LimbDollLayout.Height) * Scale;

    private void OnClick(GUIBoundKeyEventArgs args)
    {
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        var full = PixelSize;
        var margin = full * 0.18f;
        var inner = full - margin * 2f;
        var local = args.RelativePixelPosition - margin;
        if (local.X < 0f || local.Y < 0f || local.X > inner.X || local.Y > inner.Y)
        {
            Selected = null;
            OnZone?.Invoke(null);
            args.Handle();
            return;
        }

        var span = MathF.Max(1f, inner.X);
        var zone = LimbDollLayout.Hit(local / span * LimbDollLayout.Width);
        Selected = zone;
        OnZone?.Invoke(zone);
        args.Handle();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var full = PixelSize;
        var margin = full * 0.18f;
        var inner = UIBox2.FromDimensions(margin, full - margin * 2f);
        Blit(handle, LimbDollLayout.Block, UIBox2.FromDimensions(Vector2.Zero, full), Color.White);
        foreach (var icon in LimbDollLayout.Icons)
            Blit(handle, icon.Body, inner, Color.White);

        foreach (var icon in LimbDollLayout.Marks)
            Blit(handle, icon.Body, inner, Color.White);

        if (Selected is { } zone && LimbDollLayout.TryIcon(zone, out var hot) && hot.Hot.Length > 0)
            Blit(handle, hot.Hot, inner, Color.White);
    }

    private void Blit(DrawingHandleScreen handle, string state, UIBox2 box, Color color)
    {
        if (state.Length == 0 || !Sheet().TryGetState(state, out var rsi))
            return;

        handle.DrawTextureRect(rsi.GetFrame(RsiDirection.South, 0), box, color);
    }

    private RSI Sheet()
    {
        return _sheet ??= IoCManager.Resolve<IResourceCache>().GetResource<RSIResource>(LimbDollLayout.Texture).RSI;
    }
}
