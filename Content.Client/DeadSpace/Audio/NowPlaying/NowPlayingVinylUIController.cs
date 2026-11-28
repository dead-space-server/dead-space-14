// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using System.Collections.Generic;
using System.IO;
using Content.Client.Resources;
using Content.Shared.Audio;
using Content.Shared.CCVar;
using Content.Shared.DeadSpace.Administration.Events;
using Content.Shared.GameTicking;
using Robust.Client.Audio;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Graphics;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client.DeadSpace.Audio.NowPlaying;

public sealed class NowPlayingVinylUIController : UIController
{
    private static readonly ResPath VinylRsiPath = new("/Textures/_DeadSpace/Interface/NowPlaying/vinyl.rsi");
    private const string VinylState = "vinyl";
    private const string LabelState = "label";

    private const string TitleFontPath = "/Fonts/NotoSansDisplay/NotoSansDisplay-Bold.ttf";
    private const string ArtistFontPath = "/Fonts/NotoSansDisplay/NotoSansDisplay-Regular.ttf";
    private const int TitleFontSize = 15;
    private const int ArtistFontSize = 11;

    private const float DiscSize = 236f;
    private const float CornerMargin = 14f;

    private const float PeekWidth = DiscSize * 0.25f;
    private const float OffscreenOffset = DiscSize + CornerMargin + 12f;
    private const float PeekOffset = CornerMargin + DiscSize - PeekWidth;

    private const float EnterTime = 0.55f;
    private const float PeekTime = 0.5f;
    private const float ExpandTime = 0.32f;
    private const float ExitTime = 0.45f;
    private const float ShowTime = 10f;
    private const float HoverGrace = 0.6f;

    private const float SpinSpeed = 3.5f;
    private const float SpinAccel = 16f;
    private const float EndDelay = 0.15f;

    private const int MaxTrackBytes = 32 * 1024 * 1024;
    private const int CoverTextureSize = 256;

    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IResourceManager _res = default!;
    [UISystemDependency] private readonly AudioSystem _audio = default!;

    private readonly Dictionary<string, TrackInfo?> _trackInfoCache = new(StringComparer.OrdinalIgnoreCase);

    private VinylNowPlayingControl? _vinyl;
    private Texture? _discTexture;
    private Texture? _labelTexture;
    private OwnedTexture? _coverTexture;
    private string? _coverPath;

    private NowPlayingPhase _phase = NowPlayingPhase.Hidden;
    private float _phaseTime;
    private float _phaseDuration = 1f;
    private float _fromOffsetX;
    private float _fromOffsetY;
    private float _fromAlpha;
    private float _toOffsetX;
    private float _toOffsetY;
    private float _toAlpha;

    private float _offsetX = OffscreenOffset;
    private float _offsetY;
    private float _alpha;
    private float _angle;
    private float _spin;
    private float _hideTimer = ShowTime;
    private float _remaining;
    private bool _countdown;
    private bool _paused;
    private bool _hovered;
    private int _streamId;
    private bool _adminSoundsEnabled = true;
    private bool _vinylBroken;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<AdminSoundEvent>(OnAdminSound);
        SubscribeNetworkEvent<AdminSoundPlaybackControlEvent>(OnPlaybackControl);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        _cfg.OnValueChanged(CCVars.AdminSoundsEnabled, OnAdminSoundsEnabledChanged, true);
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_phase == NowPlayingPhase.Hidden)
            return;

        var frameTime = args.DeltaSeconds;

        UpdateHover();

        if (_phase is NowPlayingPhase.Entering
            or NowPlayingPhase.PeekCollapsing
            or NowPlayingPhase.Expanding
            or NowPlayingPhase.Leaving)
        {
            AdvanceAnimation(frameTime);
        }
        else if (_phase == NowPlayingPhase.Shown)
        {
            _offsetX = 0f;
            _offsetY = 0f;
            _alpha = 1f;

            if (!_hovered && !_paused)
                _hideTimer -= frameTime;

            if (_hideTimer <= 0f)
                StartPeek();
        }

        if (_countdown && !_paused)
        {
            _remaining -= frameTime;
            if (_remaining <= 0f)
                BeginExit();
        }

        var spinTarget = _paused || _phase == NowPlayingPhase.Leaving ? 0f : SpinSpeed;
        if (MathF.Abs(spinTarget - _spin) <= SpinAccel * frameTime)
            _spin = spinTarget;
        else
            _spin += MathF.Sign(spinTarget - _spin) * SpinAccel * frameTime;

        _angle += _spin * frameTime;
        if (_angle > MathF.Tau)
            _angle -= MathF.Tau;

        UpdateVinyl();
    }

    private void OnAdminSound(AdminSoundEvent ev, EntitySessionEventArgs args)
    {
        if (!_adminSoundsEnabled)
            return;

        var path = GetSoundPath(ev.Specifier);
        var info = path == null ? null : GetTrackInfo(path);

        var title = info?.Title ?? GetFallbackTitle(path);
        var author = info?.Author ?? string.Empty;

        if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(author) && info?.Cover == null)
            return;

        var isNewTrack = ev.StreamId != _streamId || _phase is NowPlayingPhase.Hidden or NowPlayingPhase.Leaving;
        _streamId = ev.StreamId;
        _paused = ev.Paused;

        var audio = ev.AudioParams ?? AudioParams.Default;
        var length = GetTrackLength(ev.Specifier);
        _countdown = !audio.Loop && length > 0f;
        _remaining = MathF.Max(0f, length - audio.PlayOffsetSeconds) + EndDelay;

        UpdateCover(path, info);

        if (EnsureVinyl() is not { } vinyl)
            return;

        vinyl.TrackTitle = title;
        vinyl.TrackAuthor = author;
        vinyl.CoverTexture = _coverTexture;
        vinyl.Visible = true;

        if (!isNewTrack)
        {
            UpdateVinyl();
            return;
        }

        _hideTimer = ShowTime;

        if (_phase is NowPlayingPhase.Shown or NowPlayingPhase.Expanding && _offsetX <= 1f)
            _phase = NowPlayingPhase.Shown;
        else
            StartPhase(NowPlayingPhase.Entering, 0f, 0f, 1f, EnterTime);

        UpdateVinyl();
    }

    private void OnPlaybackControl(AdminSoundPlaybackControlEvent ev, EntitySessionEventArgs args)
    {
        if (_phase is NowPlayingPhase.Hidden or NowPlayingPhase.Leaving || ev.StreamId != _streamId)
            return;

        switch (ev.Action)
        {
            case AdminGlobalSoundControl.Pause:
                _paused = true;
                break;
            case AdminGlobalSoundControl.Resume:
                _paused = false;
                break;
            case AdminGlobalSoundControl.Stop:
            case AdminGlobalSoundControl.FadeOut:
                BeginExit();
                break;
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev, EntitySessionEventArgs args)
    {
        HideVinyl();
    }

    private void OnAdminSoundsEnabledChanged(bool enabled)
    {
        _adminSoundsEnabled = enabled;

        if (!enabled)
            HideVinyl();
    }

    private void UpdateHover()
    {
        if (_vinyl == null || _vinyl.Disposed || !_vinyl.Visible)
        {
            _hovered = false;
            return;
        }

        var rect = _vinyl.GlobalRect;
        var radius = rect.Width / 2f;
        var delta = UIManager.MousePositionScaled.Position - rect.Center;
        var hovered = delta.LengthSquared() <= radius * radius;

        if (hovered == _hovered)
            return;

        _hovered = hovered;

        if (hovered)
        {
            if (_phase is NowPlayingPhase.Peeking or NowPlayingPhase.PeekCollapsing)
                StartPhase(NowPlayingPhase.Expanding, 0f, 0f, 1f, ExpandTime);
        }
        else if (_phase is NowPlayingPhase.Shown or NowPlayingPhase.Expanding)
        {
            _hideTimer = MathF.Max(_hideTimer, HoverGrace);
        }
    }

    private void StartPeek()
    {
        if (_phase is NowPlayingPhase.Peeking or NowPlayingPhase.PeekCollapsing)
            return;

        StartPhase(NowPlayingPhase.PeekCollapsing, PeekOffset, 0f, 1f, PeekTime);
    }

    private void BeginExit()
    {
        if (_phase is NowPlayingPhase.Hidden or NowPlayingPhase.Leaving)
            return;

        _countdown = false;
        StartPhase(NowPlayingPhase.Leaving, OffscreenOffset, 0f, 0f, ExitTime);
    }

    private void StartPhase(NowPlayingPhase phase, float toX, float toY, float toAlpha, float duration)
    {
        _phase = phase;
        _phaseTime = 0f;
        _phaseDuration = MathF.Max(0.001f, duration);

        _fromOffsetX = _offsetX;
        _fromOffsetY = _offsetY;
        _fromAlpha = _alpha;

        _toOffsetX = toX;
        _toOffsetY = toY;
        _toAlpha = toAlpha;
    }

    private void AdvanceAnimation(float frameTime)
    {
        _phaseTime += frameTime;

        var progress = Math.Clamp(_phaseTime / _phaseDuration, 0f, 1f);
        var eased = _phase switch
        {
            NowPlayingPhase.Entering or NowPlayingPhase.Expanding => EaseOutCubic(progress),
            NowPlayingPhase.PeekCollapsing => EaseInOutCubic(progress),
            _ => EaseInCubic(progress),
        };

        _offsetX = Lerp(_fromOffsetX, _toOffsetX, eased);
        _offsetY = Lerp(_fromOffsetY, _toOffsetY, eased);
        _alpha = Lerp(_fromAlpha, _toAlpha, eased);

        if (progress < 1f)
            return;

        switch (_phase)
        {
            case NowPlayingPhase.Entering:
                _phase = NowPlayingPhase.Shown;
                _hideTimer = ShowTime;
                _alpha = 1f;
                break;
            case NowPlayingPhase.PeekCollapsing:
                _phase = NowPlayingPhase.Peeking;
                _offsetX = PeekOffset;
                break;
            case NowPlayingPhase.Expanding:
                _phase = NowPlayingPhase.Shown;
                _hideTimer = MathF.Max(_hideTimer, HoverGrace);
                break;
            case NowPlayingPhase.Leaving:
                HideVinyl();
                break;
        }
    }

    private void HideVinyl()
    {
        _phase = NowPlayingPhase.Hidden;
        _countdown = false;
        _paused = false;
        _hovered = false;
        _streamId = 0;
        _spin = 0f;
        _offsetX = OffscreenOffset;
        _offsetY = 0f;
        _alpha = 0f;
        _hideTimer = ShowTime;

        if (_vinyl != null && !_vinyl.Disposed)
            _vinyl.Visible = false;
    }

    private void UpdateVinyl()
    {
        if (EnsureVinyl() is not { } vinyl)
            return;

        vinyl.Visible = true;
        vinyl.DiscAngle = _angle;
        vinyl.LabelTexture = _labelTexture;
        vinyl.CoverTexture = _coverTexture;
        vinyl.Modulate = Color.White.WithAlpha(Math.Clamp(_alpha, 0f, 1f));

        LayoutContainer.SetMarginLeft(vinyl, CornerMargin - DiscSize + _offsetX);
        LayoutContainer.SetMarginRight(vinyl, CornerMargin + _offsetX);
        LayoutContainer.SetMarginTop(vinyl, CornerMargin - DiscSize + _offsetY);
        LayoutContainer.SetMarginBottom(vinyl, CornerMargin + _offsetY);
    }

    private VinylNowPlayingControl? EnsureVinyl()
    {
        if (_vinylBroken)
            return null;

        var screen = UIManager.ActiveScreen;
        if (screen == null)
            return null;

        if (_vinyl != null && !_vinyl.Disposed && _vinyl.Parent == screen)
            return _vinyl;

        _vinyl = null;

        try
        {
            _discTexture ??= LoadVinylTexture(VinylState);
            if (_discTexture == null)
            {
                _vinylBroken = true;
                Log.Error($"Не найден спрайт винила: {VinylRsiPath}:{VinylState}");
                return null;
            }

            _labelTexture ??= LoadVinylTexture(LabelState);
            if (_labelTexture == null)
                Log.Error($"Не найден спрайт этикетки: {VinylRsiPath}:{LabelState}");

            var vinyl = new VinylNowPlayingControl(
                _cache.GetFont(TitleFontPath, TitleFontSize),
                _cache.GetFont(ArtistFontPath, ArtistFontSize))
            {
                DiscTexture = _discTexture,
                LabelTexture = _labelTexture,
                CoverTexture = _coverTexture,
                DiscAngle = _angle,
                HorizontalAlignment = Control.HAlignment.Stretch,
                VerticalAlignment = Control.VAlignment.Stretch,
            };

            LayoutContainer.SetAnchorLeft(vinyl, 1f);
            LayoutContainer.SetAnchorRight(vinyl, 1f);
            LayoutContainer.SetAnchorTop(vinyl, 1f);
            LayoutContainer.SetAnchorBottom(vinyl, 1f);
            LayoutContainer.SetMarginLeft(vinyl, CornerMargin - DiscSize + _offsetX);
            LayoutContainer.SetMarginRight(vinyl, CornerMargin + _offsetX);
            LayoutContainer.SetMarginTop(vinyl, CornerMargin - DiscSize + _offsetY);
            LayoutContainer.SetMarginBottom(vinyl, CornerMargin + _offsetY);

            screen.AddChild(vinyl);
            _vinyl = vinyl;
            return vinyl;
        }
        catch (Exception e)
        {
            _vinylBroken = true;
            Log.Error($"Не удалось создать винил проигрываемого трека: {e}");
            return null;
        }
    }

    private Texture? LoadVinylTexture(string state)
    {
        if (!_cache.TryGetResource(VinylRsiPath, out RSIResource? rsi))
            return null;

        if (!rsi.RSI.TryGetState(state, out var rsiState))
            return null;

        return rsiState.Frame0;
    }

    private void UpdateCover(string? path, TrackInfo? info)
    {
        if (_coverPath == path)
            return;

        _coverPath = path;
        _coverTexture?.Dispose();
        _coverTexture = LoadCoverTexture(info?.Cover);

        if (_vinyl != null && !_vinyl.Disposed)
            _vinyl.CoverTexture = _coverTexture;
    }

    private OwnedTexture? LoadCoverTexture(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return null;

        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var source = Image.Load<Rgba32>(stream);

            var side = Math.Min(source.Width, source.Height);
            if (side <= 0)
                return null;

            using var prepared = source.Clone(ctx =>
            {
                ctx.Crop(side, side);
                ctx.Resize(new Size(CoverTextureSize, CoverTextureSize));
            });

            var parameters = new TextureLoadParameters
            {
                SampleParameters = new TextureSampleParameters
                {
                    Filter = true,
                    WrapMode = TextureWrapMode.None,
                },
                Srgb = true,
                Preload = false,
            };

            return _clyde.LoadTextureFromImage(prepared, "now-playing-cover", parameters);
        }
        catch (Exception e)
        {
            Log.Debug($"Не удалось подготовить обложку трека: {e.Message}");
            return null;
        }
    }

    private static string? GetSoundPath(ResolvedSoundSpecifier specifier)
    {
        return specifier is ResolvedPathSpecifier path ? path.Path.ToString() : null;
    }

    private static string GetFallbackTitle(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        return new ResPath(path).FilenameWithoutExtension.Replace('_', ' ');
    }

    private float GetTrackLength(ResolvedSoundSpecifier specifier)
    {
        try
        {
            if (_audio == null)
                return 0f;

            return (float) _audio.GetAudioLength(specifier).TotalSeconds;
        }
        catch (Exception e)
        {
            Log.Debug($"Не удалось узнать длительность трека для винила: {e.Message}");
            return 0f;
        }
    }

    private TrackInfo? GetTrackInfo(string path)
    {
        if (_trackInfoCache.TryGetValue(path, out var cached))
            return cached;

        TrackInfo? info = null;

        try
        {
            if (_res.TryContentFileRead(path, out var stream))
            {
                using (stream)
                {
                    var data = ReadTrackFile(stream);
                    if (data.Length > 0 &&
                        VorbisCommentMetadata.TryReadMetadata(data, out var title, out var artist, out var cover))
                    {
                        info = new TrackInfo(title, artist, cover);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Log.Debug($"Не удалось прочитать метаданные {path}: {e.Message}");
        }

        if (_trackInfoCache.Count > 16)
            _trackInfoCache.Clear();

        _trackInfoCache[path] = info;
        return info;
    }

    private static byte[] ReadTrackFile(Stream stream)
    {
        if (stream.CanSeek && stream.Length is > 0 and <= int.MaxValue)
        {
            var length = (int) Math.Min(stream.Length, MaxTrackBytes);
            var exact = new byte[length];
            var read = 0;

            while (read < length)
            {
                var chunk = stream.Read(exact, read, length - read);
                if (chunk <= 0)
                    break;

                read += chunk;
            }

            return read == length ? exact : exact[..read];
        }

        using var memory = new MemoryStream();
        var buffer = new byte[81920];

        while (memory.Length < MaxTrackBytes)
        {
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read <= 0)
                break;

            memory.Write(buffer, 0, read);
        }

        return memory.ToArray();
    }

    private static float EaseOutCubic(float value)
    {
        var inverse = 1f - value;
        return 1f - inverse * inverse * inverse;
    }

    private static float EaseInCubic(float value)
    {
        return value * value * value;
    }

    private static float EaseInOutCubic(float value)
    {
        return value < 0.5f
            ? 4f * value * value * value
            : 1f - MathF.Pow(-2f * value + 2f, 3f) / 2f;
    }

    private static float Lerp(float from, float to, float value)
    {
        return from + (to - from) * value;
    }

    private sealed record TrackInfo(string? Title, string? Author, byte[]? Cover);

    private enum NowPlayingPhase : byte
    {
        Hidden,
        Entering,
        Shown,
        PeekCollapsing,
        Peeking,
        Expanding,
        Leaving,
    }
}
