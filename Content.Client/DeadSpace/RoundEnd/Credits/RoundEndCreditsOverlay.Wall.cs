// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.DeadSpace.RoundEnd.Credits;

public sealed partial class RoundEndCreditsOverlay
{
    private const float WallTileWidth = 200f;
    private const float WallRevealSeconds = 6f;
    private const float WallTileFadeSeconds = 0.5f;
    private const float WallScrollSpeed = 30f;
    private const float WallManualPause = 6f;

    private BoxContainer? _wall;
    private ScrollContainer? _wallScroll;
    private GridContainer? _wallGrid;
    private Label? _wallFooter;
    private readonly List<(Control Tile, Label Name, Color Color)> _wallTiles = new();
    private float _wallInterval;
    private float _wallAutoScroll;
    private float _wallLastScroll;
    private float _wallManualUntil;
    private float _wallDirection = 1f;

    private void BuildWall()
    {
        _wall = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 10,
            MouseFilter = MouseFilterMode.Pass,
            Modulate = Color.Transparent,
        };

        _wall.AddChild(MakeLabel(Loc.GetString("round-end-credits-wall-title").ToUpperInvariant(), 30, Gold, BoldFont));
        _wall.AddChild(MakeLabel(Loc.GetString("round-end-credits-wall-subtitle", ("roundId", _round.RoundId)), 14, DimColor));

        var players = _round.AllPlayersEndInfo
            .Where(p => !string.IsNullOrWhiteSpace(p.PlayerOOCName))
            .GroupBy(p => p.PlayerOOCName)
            .Select(g => g.OrderBy(IsPureObserver).First())
            .OrderBy(IsPureObserver)
            .ThenBy(p => GetDisplayName(p), StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var width = GetCenterWidth(ScreenWidth);
        _wallGrid = new GridContainer
        {
            Columns = Math.Clamp((int) (width / (WallTileWidth + 10f)), 2, 8),
            HSeparationOverride = 10,
            VSeparationOverride = 8,
            HorizontalAlignment = HAlignment.Center,
            MouseFilter = MouseFilterMode.Ignore,
        };

        foreach (var info in players)
        {
            var color = info.Antag ? Blood : IsPureObserver(info) ? DimColor : TextColor;
            var tile = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                SetWidth = WallTileWidth,
                MouseFilter = MouseFilterMode.Ignore,
                Modulate = Color.Transparent,
            };
            var name = MakeLabel(GetDisplayName(info), 14, Gold, BoldFont);
            name.ClipText = true;
            var ckey = MakeLabel(info.PlayerOOCName, 12, Cyan);
            ckey.ClipText = true;
            tile.AddChild(name);
            tile.AddChild(ckey);
            _wallGrid.AddChild(tile);
            _wallTiles.Add((tile, name, color));
        }

        _wallScroll = new ScrollContainer
        {
            HScrollEnabled = false,
            VerticalExpand = true,
            HorizontalExpand = true,
        };
        _wallScroll.AddChild(_wallGrid);
        _wall.AddChild(_wallScroll);

        _wallFooter = MakeLabel(Loc.GetString("round-end-credits-outro-thanks"), 18, TextColor, BoldFont);
        _wall.AddChild(_wallFooter);

        _wallInterval = _wallTiles.Count == 0
            ? 0f
            : Math.Clamp(WallRevealSeconds / _wallTiles.Count, 0.03f, 0.18f);

        AddChild(_wall);
        SetAnchorPreset(_wall, LayoutPreset.Wide);
        var side = (ScreenWidth - width) / 2f;
        SetMarginLeft(_wall, side);
        SetMarginRight(_wall, -side);
        SetMarginTop(_wall, 40f);
        SetMarginBottom(_wall, -40f);
    }

    private void UpdateWall(float time, float frameTime)
    {
        if (_wall == null)
            BuildWall();

        if (_wall == null)
            return;

        _wall.Modulate = Color.White.WithAlpha(Math.Clamp(time / 1.2f, 0f, 1f));

        for (var i = 0; i < _wallTiles.Count; i++)
        {
            var (tile, name, color) = _wallTiles[i];
            var local = time - 0.8f - i * _wallInterval;
            var alpha = Math.Clamp(local / WallTileFadeSeconds, 0f, 1f);
            tile.Modulate = Color.White.WithAlpha(alpha);

            var glow = Math.Clamp(local / 1.5f, 0f, 1f);
            name.FontColorOverride = Color.InterpolateBetween(Gold, color, glow);
        }

        UpdateWallScroll(time, frameTime);

        if (_wallFooter == null)
            return;

        var footerTime = time - 1.5f - _wallTiles.Count * _wallInterval;
        var footerAlpha = Math.Clamp(footerTime, 0f, 1f) * (0.75f + 0.25f * MathF.Sin(time * 2f));
        _wallFooter.FontColorOverride = TextColor.WithAlpha(footerAlpha);
    }

    private void UpdateWallScroll(float time, float frameTime)
    {
        if (_wallScroll == null || _wallGrid == null)
            return;

        var overflow = _wallGrid.Size.Y - _wallScroll.Size.Y;
        if (overflow <= 1f)
            return;

        var current = _wallScroll.GetScrollValue().Y;
        if (MathF.Abs(current - _wallLastScroll) > 1f)
        {
            _wallManualUntil = time + WallManualPause;
            _wallAutoScroll = current;
        }

        if (time >= _wallManualUntil && time > WallRevealSeconds)
        {
            _wallAutoScroll += _wallDirection * WallScrollSpeed * frameTime;
            if (_wallAutoScroll >= overflow)
            {
                _wallAutoScroll = overflow;
                _wallDirection = -1f;
            }
            else if (_wallAutoScroll <= 0f)
            {
                _wallAutoScroll = 0f;
                _wallDirection = 1f;
            }

            _wallScroll.SetScrollValue(new Vector2(0, _wallAutoScroll));
        }

        _wallLastScroll = _wallScroll.GetScrollValue().Y;
    }
}
