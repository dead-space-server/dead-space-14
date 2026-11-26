// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Shared.GameTicking.Prototypes;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Lobby;

/// <summary>
/// The lobby arts behind the "Dead Space" lobby when the art background is chosen: every art in turn, a minute each,
/// crossfading into the next, covering the page and dimmed a little so the panels stay readable.
/// </summary>
public sealed partial class HologramLobbyArt : Control
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private IRobustRandom _random = default!;

    private const float HoldSeconds = 60f;
    private const float FadeSeconds = 2.5f;
    private static readonly Color Dim = Color.Black.WithAlpha(0.35f);

    private readonly List<ResPath> _arts = new();
    private int _index;
    private float _time;

    public HologramLobbyArt()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();

        if (_arts.Count > 0)
            return;

        _arts.AddRange(_prototypes.EnumeratePrototypes<LobbyBackgroundPrototype>()
            .Select(proto => proto.Background)
            .OrderBy(path => path.ToString()));

        if (_arts.Count > 0)
            _index = _random.Next(_arts.Count);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!VisibleInTree || _arts.Count < 2)
            return;

        _time += args.DeltaSeconds;
        if (_time < HoldSeconds + FadeSeconds)
            return;

        _time -= HoldSeconds + FadeSeconds;
        _index = (_index + 1) % _arts.Count;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (_arts.Count == 0)
            return;

        DrawCover(handle, _arts[_index], Color.White);

        // The next art fades in over the last seconds of this one.
        if (_arts.Count > 1 && _time > HoldSeconds)
        {
            var alpha = Math.Clamp((_time - HoldSeconds) / FadeSeconds, 0f, 1f);
            alpha = alpha * alpha * (3f - 2f * alpha);
            DrawCover(handle, _arts[(_index + 1) % _arts.Count], Color.White.WithAlpha(alpha));
        }

        handle.DrawRect(PixelSizeBox, Dim);
    }

    /// <summary>Draws an art scaled to cover the whole control, centred, cropping what does not fit.</summary>
    private void DrawCover(DrawingHandleScreen handle, ResPath path, Color modulate)
    {
        if (!_resources.TryGetResource<TextureResource>(path, out var resource))
            return;

        var texture = resource.Texture;
        var size = (Vector2) PixelSize;
        var scale = MathF.Max(size.X / texture.Width, size.Y / texture.Height);
        var drawn = new Vector2(texture.Width, texture.Height) * scale;
        var topLeft = (size - drawn) / 2f;
        handle.DrawTextureRect(texture, UIBox2.FromDimensions(topLeft, drawn), modulate);
    }
}
