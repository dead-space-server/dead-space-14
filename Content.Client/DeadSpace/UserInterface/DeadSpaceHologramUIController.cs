// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.DeadSpace.Stylesheets;
using Content.Client.Strip;
using Content.Client.UserInterface.Systems.Storage.Controls;
using Content.Shared.CCVar;
using Content.Shared.DeadSpace.CCCCVars;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.UserInterface;

/// <summary>
/// Hologram effects of the "Dead Space" interface style: a window is projected in with a sweeping line and a short
/// flicker, then a faint scanline crawls over it and it blinks now and then; a closed window's projection collapses
/// into a line and goes out. Strength, timings and which effects run are player settings (<c>ui.hologram_*</c>
/// cvars), the colour follows the style's projection colour; everything is off with reduced motion.
/// </summary>
public sealed partial class DeadSpaceHologramUIController : UIController
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IRobustRandom _random = default!;

    /// <summary>Part of the close spent collapsing to a line; the rest shrinks the line to a point.</summary>
    private const float CloseCollapse = 0.6f;
    private const float FlickerDuration = 0.09f;
    private const float FlickerDim = 0.18f;
    private const float FlickerMinDelay = 4f;
    private const float FlickerMaxDelay = 10f;
    private const float ScanPeriod = 5f;
    private const float ScanBand = 28f;
    private const float SweepGlow = 6f;
    private const int GlowSteps = 6;

    /// <summary>Window opacity over the projection, read left to right.</summary>
    private static readonly float[] OpenPattern = [0.25f, 0.6f, 0.2f, 0.75f, 0.5f, 0.9f, 0.7f, 1f];

    private static Color Projection => DeadSpaceStylePalette.BorderHeader;
    private static Color ProjectionBright => DeadSpaceStylePalette.CyanBright;

    private readonly Dictionary<BaseWindow, HologramState> _windows = [];
    private readonly List<UIBox2> _occluders = [];
    private readonly List<ClosingGhost> _closing = [];

    private float _strength;
    private float _openDuration;
    private float _closeDuration;
    private bool _scanline;
    private bool _flicker;
    private bool _reducedMotion;
    private float _time;

    private bool Active => DeadSpaceStylePalette.Hologram && _strength > 0f && !_reducedMotion;

    public override void Initialize()
    {
        base.Initialize();

        _cfg.OnValueChanged(CCCCVars.HologramEffects, value => _strength = Math.Clamp(value, 0f, 1f), true);
        _cfg.OnValueChanged(CCCCVars.HologramOpenTime, value => _openDuration = Math.Max(value, 0) / 1000f, true);
        _cfg.OnValueChanged(CCCCVars.HologramCloseTime, value => _closeDuration = Math.Max(value, 0) / 1000f, true);
        _cfg.OnValueChanged(CCCCVars.HologramScanline, value => _scanline = value, true);
        _cfg.OnValueChanged(CCCCVars.HologramFlicker, value => _flicker = value, true);
        _cfg.OnValueChanged(CCVars.ReducedMotion, value => _reducedMotion = value, true);

        UIManager.WindowRoot.OnChildAdded += OnWindowAdded;
        UIManager.WindowRoot.OnChildRemoved += OnWindowRemoved;
        UIManager.OnPostDrawUIRoot += Draw;

        foreach (var child in UIManager.WindowRoot.Children)
            OnWindowAdded(child);
    }

    private void OnWindowAdded(Control control)
    {
        if (control is not BaseWindow window || IsInstant(window) || _windows.ContainsKey(window))
            return;

        var state = new HologramState
        {
            // A window opened while the effects are off just appears.
            Opening = Active && _openDuration > 0f,
            NextFlicker = NextFlickerDelay(),
            ScanOffset = _random.NextFloat(ScanPeriod),
        };
        _windows[window] = state;

        if (state.Opening)
            Apply(window, state, Scale(OpenPattern[0]));
    }

    /// <summary>
    /// Bags, boxes and other containers, and someone else's inventory, are opened and closed all the time mid-action:
    /// they appear and vanish at once, with no projection effects at all.
    /// </summary>
    private static bool IsInstant(BaseWindow window)
    {
        return window is StorageWindow or StrippingMenu;
    }

    private void OnWindowRemoved(Control control)
    {
        if (control is not BaseWindow window || !_windows.Remove(window, out var state))
            return;

        Restore(window, state);

        // The window is already out of the tree and its contents can't be drawn, so its projection goes out in its
        // place. The rect is the one last drawn: after removal the window no longer knows its UI scale.
        if (Active && _closeDuration > 0f && state.LastRect.Width > 1f && state.LastRect.Height > 1f)
            _closing.Add(new ClosingGhost { Rect = state.LastRect, Scale = state.LastScale });
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var active = Active;
        _time += args.DeltaSeconds;

        if (!active)
            _closing.Clear();

        for (var i = _closing.Count - 1; i >= 0; i--)
        {
            _closing[i].Age += args.DeltaSeconds;
            if (_closing[i].Age >= _closeDuration)
                _closing.RemoveAt(i);
        }

        foreach (var (window, state) in _windows)
        {
            if (!active)
            {
                state.Opening = false;
                state.FlickerLeft = 0f;
                Restore(window, state);
                continue;
            }

            var alpha = 1f;
            if (state.Opening)
            {
                state.Age += args.DeltaSeconds;
                var progress = _openDuration > 0f ? Math.Clamp(state.Age / _openDuration, 0f, 1f) : 1f;
                alpha = OpenPattern[Math.Min((int) (progress * OpenPattern.Length), OpenPattern.Length - 1)];
                state.Opening = progress < 1f;
            }
            else if (!_flicker)
            {
                state.FlickerLeft = 0f;
            }
            else if (state.FlickerLeft > 0f)
            {
                state.FlickerLeft -= args.DeltaSeconds;
                alpha = 1f - FlickerDim;
            }
            else
            {
                state.NextFlicker -= args.DeltaSeconds;
                if (state.NextFlicker <= 0f)
                {
                    state.FlickerLeft = FlickerDuration;
                    state.NextFlicker = NextFlickerDelay();
                }
            }

            Apply(window, state, Scale(alpha));
        }
    }

    private void Draw(PostDrawUIRootEventArgs args)
    {
        if (!Active || args.Root != UIManager.RootControl || (_windows.Count == 0 && _closing.Count == 0))
            return;

        var handle = args.DrawingHandle;
        var scale = args.Root.UIScale;
        var windowRoot = UIManager.WindowRoot;

        // Effects are drawn after the whole UI, so anything stacked over a window must not get its scanline.
        _occluders.Clear();
        foreach (var popup in UIManager.PopupRoot.Children)
        {
            if (popup.Visible)
                _occluders.Add(PixelRect(popup));
        }

        for (var i = windowRoot.ChildCount - 1; i >= 0; i--)
        {
            var child = windowRoot.GetChild(i);
            if (!child.Visible)
                continue;

            var rect = PixelRect(child);
            if (child is BaseWindow window
                && _windows.TryGetValue(window, out var state)
                && rect.Width > 1f
                && rect.Height > 1f)
            {
                state.LastRect = rect;
                state.LastScale = scale;

                if (state.Opening)
                    DrawSweep(handle, rect, _openDuration > 0f ? state.Age / _openDuration : 1f, scale);
                else if (_scanline)
                    DrawScanline(handle, rect, state, scale);
            }

            _occluders.Add(rect);
        }

        foreach (var ghost in _closing)
        {
            DrawClose(handle, ghost);
        }
    }

    /// <summary>A closed window's projection: collapses to a bright line through its middle, then to a point.</summary>
    private void DrawClose(DrawingHandleScreen handle, ClosingGhost ghost)
    {
        var rect = ghost.Rect;
        var center = rect.Center;
        var progress = _closeDuration > 0f ? Math.Clamp(ghost.Age / _closeDuration, 0f, 1f) : 1f;
        var line = MathF.Max(1f, 2f * ghost.Scale);

        if (progress < CloseCollapse)
        {
            var p = progress / CloseCollapse;
            var eased = p * p;
            var half = MathF.Max(line / 2f, rect.Height / 2f * (1f - eased));
            var box = new UIBox2(rect.Left, center.Y - half, rect.Right, center.Y + half);

            handle.DrawRect(box, DeadSpaceStylePalette.Surface.WithAlpha(0.6f * _strength * (1f - p * 0.5f)));
            handle.DrawRect(box, Projection.WithAlpha(0.85f * _strength), filled: false);
            FillClipped(handle, box, center.Y - line / 2f, center.Y + line / 2f,
                ProjectionBright.WithAlpha(0.9f * _strength * p));
            return;
        }

        var q = (progress - CloseCollapse) / (1f - CloseCollapse);
        var halfWidth = MathF.Max(line, rect.Width / 2f * (1f - q * q));
        handle.DrawRect(
            new UIBox2(center.X - halfWidth, center.Y - line / 2f, center.X + halfWidth, center.Y + line / 2f),
            ProjectionBright.WithAlpha(0.9f * _strength * (1f - q * 0.6f)));
    }

    /// <summary>The projection line running down the window while it opens.</summary>
    private void DrawSweep(DrawingHandleScreen handle, UIBox2 rect, float progress, float scale)
    {
        progress = Math.Clamp(progress, 0f, 1f);
        var eased = 1f - (1f - progress) * (1f - progress);
        var y = rect.Top + rect.Height * eased;
        var glow = SweepGlow * scale;
        var step = glow / GlowSteps;

        for (var i = 0; i < GlowSteps; i++)
        {
            var alpha = 0.10f * _strength * (1f - (float) i / GlowSteps);
            FillClipped(handle, rect, y - step * (i + 1), y - step * i, Projection.WithAlpha(alpha));
        }

        var line = MathF.Max(1f, 2f * scale);
        FillClipped(handle, rect, y - line, y, ProjectionBright.WithAlpha(0.6f * _strength));
    }

    /// <summary>A faint band slowly crawling down an open window.</summary>
    private void DrawScanline(DrawingHandleScreen handle, UIBox2 rect, HologramState state, float scale)
    {
        var band = ScanBand * scale;
        var travel = rect.Height + band;
        var phase = ((_time + state.ScanOffset) % ScanPeriod) / ScanPeriod;
        var bottom = rect.Top + travel * phase;
        var top = bottom - band;

        var bandRect = new UIBox2(rect.Left, MathF.Max(top, rect.Top), rect.Right, MathF.Min(bottom, rect.Bottom));
        if (bandRect.Height <= 0f || Occluded(bandRect))
            return;

        var step = band / GlowSteps;
        for (var i = 0; i < GlowSteps; i++)
        {
            // Brightest at the leading (lower) edge.
            var alpha = 0.035f * _strength * (1f - (float) i / GlowSteps);
            FillClipped(handle, rect, bottom - step * (i + 1), bottom - step * i, Projection.WithAlpha(alpha));
        }
    }

    private bool Occluded(UIBox2 box)
    {
        foreach (var other in _occluders)
        {
            if (box.Left < other.Right && other.Left < box.Right && box.Top < other.Bottom && other.Top < box.Bottom)
                return true;
        }

        return false;
    }

    private static void FillClipped(DrawingHandleScreen handle, UIBox2 clip, float top, float bottom, Color color)
    {
        top = MathF.Max(top, clip.Top);
        bottom = MathF.Min(bottom, clip.Bottom);
        if (bottom <= top || color.A <= 0f)
            return;

        handle.DrawRect(new UIBox2(clip.Left, top, clip.Right, bottom), color);
    }

    private static UIBox2 PixelRect(Control control)
    {
        var position = (Vector2) control.GlobalPixelPosition;
        return UIBox2.FromDimensions(position, control.PixelSize);
    }

    private float Scale(float alpha)
    {
        return 1f - (1f - alpha) * _strength;
    }

    private float NextFlickerDelay()
    {
        return _random.NextFloat(FlickerMinDelay, FlickerMaxDelay);
    }

    private static void Apply(BaseWindow window, HologramState state, float alpha)
    {
        if (alpha >= 0.999f)
        {
            Restore(window, state);
            return;
        }

        state.BaseModulate ??= window.Modulate;
        var baseModulate = state.BaseModulate.Value;
        window.Modulate = baseModulate.WithAlpha(baseModulate.A * alpha);
    }

    private static void Restore(BaseWindow window, HologramState state)
    {
        if (state.BaseModulate is not { } baseModulate)
            return;

        window.Modulate = baseModulate;
        state.BaseModulate = null;
    }

    private sealed class HologramState
    {
        public bool Opening;
        public float Age;
        public float NextFlicker;
        public float FlickerLeft;
        public float ScanOffset;
        public UIBox2 LastRect;
        public float LastScale = 1f;

        /// <summary>The window's own modulate while the effect overrides it.</summary>
        public Color? BaseModulate;
    }

    private sealed class ClosingGhost
    {
        public UIBox2 Rect;
        public float Scale;
        public float Age;
    }
}
