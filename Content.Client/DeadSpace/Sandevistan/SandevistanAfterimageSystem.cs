// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.DeadSpace.Afterimages;
using Content.Shared.DeadSpace.Sandevistan;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Sandevistan;

public sealed partial class SandevistanAfterimageSystem : EntitySystem
{
    private const int MaxAfterimagesPerFrame = 14;
    private const float MinStepDistance = 0.005f;
    private const float VisualFadeDuration = 2.5f;
    private const float SoftcapRampLeadTime = 2f;

    private static readonly Color CloseTrailColor = Color.FromHex("#b00018dc");
    private static readonly Color CloseSoftcapTrailColor = Color.FromHex("#ff1e24e6");
    private static readonly Color FarTrailColor = Color.FromHex("#38c8ffbf");
    private static readonly Color FarSoftcapTrailColor = Color.FromHex("#00f0ffd2");

    [Dependency] private DeadSpaceAfterimageSystem _afterimages = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly Dictionary<EntityUid, TrailState> _trailStates = new();
    private readonly HashSet<EntityUid> _activeThisFrame = new();
    private readonly List<EntityUid> _staleTrailStates = new();

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        _activeThisFrame.Clear();
        var curTime = _timing.CurTime;

        var query = EntityQueryEnumerator<ActiveSandevistanComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var active, out var xform))
        {
            if (Deleted(uid))
                continue;

            _activeThisFrame.Add(uid);
            UpdateTrail(uid, GetTrailVisuals(active, curTime), xform);
        }

        var fadeoutQuery = EntityQueryEnumerator<SandevistanVisualFadeoutComponent, TransformComponent>();
        while (fadeoutQuery.MoveNext(out var uid, out var fadeout, out var xform))
        {
            if (Deleted(uid) || HasComp<ActiveSandevistanComponent>(uid))
                continue;

            _activeThisFrame.Add(uid);
            UpdateTrail(uid, GetTrailVisuals(fadeout, curTime), xform);
        }

        RemoveStaleTrailStates();
    }

    private void UpdateTrail(EntityUid uid, TrailVisuals visuals, TransformComponent xform)
    {
        if (visuals.Intensity <= 0.02f)
            return;

        var current = _transform.ToMapCoordinates(xform.Coordinates);
        var currentRotation = _transform.GetWorldRotation(xform);

        if (!_trailStates.TryGetValue(uid, out var state) ||
            state.MapId != current.MapId)
        {
            _trailStates[uid] = new TrailState(current.MapId, current.Position, currentRotation, _timing.CurTime);
            return;
        }

        var delta = current.Position - state.Position;
        var distance = delta.Length();
        var stepDistance = MathF.Max(visuals.AfterimageMinDistance, MinStepDistance);

        if (distance < stepDistance)
        {
            _trailStates[uid] = state with { Rotation = currentRotation };
            return;
        }

        var curTime = _timing.CurTime;
        if (curTime < state.NextSpawnTime)
            return;

        var samples = Math.Clamp((int) MathF.Ceiling(distance / stepDistance), 1, MaxAfterimagesPerFrame);

        for (var i = 1; i <= samples; i++)
        {
            var fraction = i / (float) samples;
            var position = Vector2.Lerp(state.Position, current.Position, fraction);
            var rotation = Angle.Lerp(state.Rotation, currentRotation, fraction);
            var coordinates = _transform.ToCoordinates(new MapCoordinates(position, current.MapId));
            var (startColor, endColor) = GetTrailColors(visuals, fraction);

            _afterimages.TrySpawnAfterimage(
                uid,
                coordinates,
                rotation,
                startColor,
                visuals.AfterimageLifetime,
                visuals.AfterimageFallbackEffect,
                endColor);
        }

        _trailStates[uid] = new TrailState(
            current.MapId,
            current.Position,
            currentRotation,
            curTime + TimeSpan.FromSeconds(MathF.Max(visuals.AfterimageInterval, 0.01f)));
    }

    private static (Color Start, Color End) GetTrailColors(TrailVisuals visuals, float proximity)
    {
        var closeColor = Color.InterpolateBetween(CloseTrailColor, CloseSoftcapTrailColor, visuals.SoftcapProgress);
        var farColor = Color.InterpolateBetween(FarTrailColor, FarSoftcapTrailColor, visuals.SoftcapProgress);
        var color = Color.InterpolateBetween(farColor, closeColor, SmoothStep(proximity));
        var alpha = Math.Clamp(visuals.AfterimageColor.A, 0.45f, 0.88f) * visuals.Intensity;

        return (color.WithAlpha(alpha), farColor.WithAlpha(alpha));
    }

    private static TrailVisuals GetTrailVisuals(ActiveSandevistanComponent active, TimeSpan curTime)
    {
        return new TrailVisuals(
            active.AfterimageInterval,
            active.AfterimageMinDistance,
            active.AfterimageLifetime,
            active.AfterimageColor,
            active.AfterimageFallbackEffect,
            GetSoftcapProgress(active, curTime),
            GetActiveVisualIntensity(active, curTime));
    }

    private static TrailVisuals GetTrailVisuals(SandevistanVisualFadeoutComponent fadeout, TimeSpan curTime)
    {
        return new TrailVisuals(
            fadeout.AfterimageInterval,
            fadeout.AfterimageMinDistance,
            fadeout.AfterimageLifetime,
            fadeout.AfterimageColor,
            fadeout.AfterimageFallbackEffect,
            Math.Clamp(fadeout.SoftcapProgress, 0f, 1f),
            GetFadeoutVisualIntensity(fadeout, curTime));
    }

    private static float GetActiveVisualIntensity(ActiveSandevistanComponent active, TimeSpan curTime)
    {
        var remaining = Math.Max(0f, (float) (active.EndTime - curTime).TotalSeconds);
        return SmoothStep(Math.Clamp(remaining / VisualFadeDuration, 0f, 1f));
    }

    private static float GetFadeoutVisualIntensity(SandevistanVisualFadeoutComponent fadeout, TimeSpan curTime)
    {
        var remaining = Math.Max(0f, (float) (fadeout.EndTime - curTime).TotalSeconds);
        var fadeOut = SmoothStep(Math.Clamp(remaining / Math.Max(fadeout.Duration, 0.1f), 0f, 1f));

        return Math.Clamp(fadeout.StartIntensity, 0f, 1f) * fadeOut;
    }

    private static float GetSoftcapProgress(ActiveSandevistanComponent active, TimeSpan curTime)
    {
        var rampStart = active.SoftcapTime - TimeSpan.FromSeconds(SoftcapRampLeadTime);
        if (curTime < rampStart)
            return 0f;

        var elapsed = Math.Max(0f, (float) (curTime - rampStart).TotalSeconds);
        return SmoothStep(Math.Clamp(elapsed / SoftcapRampLeadTime, 0f, 1f));
    }

    private static float SmoothStep(float progress)
    {
        return progress * progress * (3f - 2f * progress);
    }

    private void RemoveStaleTrailStates()
    {
        _staleTrailStates.Clear();

        foreach (var uid in _trailStates.Keys)
        {
            if (!_activeThisFrame.Contains(uid) || Deleted(uid))
                _staleTrailStates.Add(uid);
        }

        foreach (var uid in _staleTrailStates)
        {
            _trailStates.Remove(uid);
        }
    }

    private readonly record struct TrailState(MapId MapId, Vector2 Position, Angle Rotation, TimeSpan NextSpawnTime);

    private readonly record struct TrailVisuals(
        float AfterimageInterval,
        float AfterimageMinDistance,
        float AfterimageLifetime,
        Color AfterimageColor,
        string AfterimageFallbackEffect,
        float SoftcapProgress,
        float Intensity);
}
