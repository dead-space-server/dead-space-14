// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Shared.DeadSpace.Weapons.Smart;
using Robust.Client.Graphics;
using Robust.Shared.Maths;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System;
using System.Numerics;

namespace Content.Client.DeadSpace.Weapons.Smart;

public sealed class SmartWeaponOverlay : Overlay
{
    private readonly SmartWeaponSystem _system;
    private readonly IGameTiming _timing;
    private readonly IRobustRandom _random = new RobustRandom();

    private TimeSpan _nextGlitchTime;
    private TimeSpan _glitchEndTime;
    private bool _isGlitching;

    public SmartWeaponOverlay(SmartWeaponSystem system, IGameTiming timing)
    {
        _system = system;
        _timing = timing;
        _nextGlitchTime = _timing.RealTime + TimeSpan.FromSeconds(_random.NextFloat(8f, 16f));
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_system.IsActive)
            return;

        var handle = args.ScreenHandle;
        var mousePos = _system.MouseScreenPosition;

        DrawDebugZone(handle, mousePos);
        DrawCrosshair(handle, mousePos);
        DrawTargetMarkers(handle);
        DrawAmmoCounter(handle);
    }

    private void DrawDebugZone(DrawingHandleScreen handle, Vector2 center)
    {
        if (!_system.ShowDebugZone)
            return;

        handle.DrawCircle(center, _system.TargetRadiusPixels, _system.DebugZoneColor);
    }

    private void DrawCrosshair(DrawingHandleScreen handle, Vector2 center)
    {
        var progress = _system.CrosshairProgress;
        var scaleFactor = 1f - (1f - _system.LockScaleFactor) * progress;
        var rotationOffset = _system.LockRotation * progress;
        var sizeMultiplier = _system.CrosshairScale;
        var alpha = _system.CrosshairAlpha;
        var time = (float)_timing.RealTime.TotalSeconds;

        var jitterX = MathF.Sin(time * 30f) * 0.7f;
        var jitterY = MathF.Cos(time * 25f) * 0.5f;
        var jitter = new Vector2(jitterX, jitterY);

        var redCenter = center + jitter * 0.4f + new Vector2(-2f, 0f);
        DrawRing(handle, redCenter, _system.InnerRingTexture, new Color(1f, 0f, 0f, 0.35f * alpha), scaleFactor * sizeMultiplier, rotationOffset);
        DrawRing(handle, redCenter, _system.OuterRingTexture, new Color(1f, 0f, 0f, 0.35f * alpha), scaleFactor * sizeMultiplier, -rotationOffset);

        var blueCenter = center + jitter * 0.6f + new Vector2(2f, 0f);
        DrawRing(handle, blueCenter, _system.InnerRingTexture, new Color(0f, 0.5f, 1f, 0.35f * alpha), scaleFactor * sizeMultiplier, rotationOffset);
        DrawRing(handle, blueCenter, _system.OuterRingTexture, new Color(0f, 0.5f, 1f, 0.35f * alpha), scaleFactor * sizeMultiplier, -rotationOffset);

        DrawRing(handle, center + jitter, _system.InnerRingTexture, new Color(0f, 1f, 1f, 0.15f * alpha), scaleFactor * sizeMultiplier, rotationOffset);
        DrawRing(handle, center + jitter, _system.OuterRingTexture, new Color(0f, 1f, 1f, 0.15f * alpha), scaleFactor * sizeMultiplier, -rotationOffset);

        DrawRing(handle, center, _system.InnerRingTexture, _system.CrosshairColor.WithAlpha(_system.CrosshairColor.A * alpha), scaleFactor * sizeMultiplier, rotationOffset);
        DrawRing(handle, center, _system.OuterRingTexture, _system.CrosshairColor.WithAlpha(_system.CrosshairColor.A * alpha), scaleFactor * sizeMultiplier, -rotationOffset);
    }

    private void DrawRing(DrawingHandleScreen handle, Vector2 center, Texture? texture, Color color, float scale, float rotationRadians)
    {
        if (texture == null)
            return;

        var size = texture.Size * scale;
        var transform = Matrix3x2.CreateRotation(rotationRadians, center);
        handle.SetTransform(transform);
        handle.DrawTextureRect(texture, UIBox2.FromDimensions(center - size / 2, size), color);
        handle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawTargetMarkers(DrawingHandleScreen handle)
    {
        foreach (var anim in _system.TargetAnimations)
        {
            if (anim.Progress <= 0f && !anim.IsLocking)
                continue;

            var targetPos = _system.GetScreenPosition(anim.Target);
            DrawSingleTargetMarker(handle, targetPos, anim.Progress, anim.IsLocking);
        }
    }

    private void DrawSingleTargetMarker(DrawingHandleScreen handle, Vector2 targetPos, float progressSeconds, bool isLocking)
    {
        if (progressSeconds <= 0f)
            return;

        float r = 24f;
        float t = progressSeconds;
        float baseDuration = _system.TargetAnimationDuration;

        float appearEnd = 0.05f * baseDuration;
        float pause1End = 0.20f * baseDuration;
        float rotateEnd = 0.35f * baseDuration;
        float crossEnd = 0.50f * baseDuration;
        float pause2End = 0.55f * baseDuration;
        float fillEnd = 0.70f * baseDuration;

        float flashAppearDuration = 0.15f;
        float flashHoldDuration = 0.05f;
        float flashDisappearDuration = 0.15f;
        float flashAppearEnd = fillEnd + flashAppearDuration;
        float flashHoldEnd = flashAppearEnd + flashHoldDuration;
        float flashEnd = flashHoldEnd + flashDisappearDuration;

        float markerAlpha = 1f;
        float markerScale = 0f;
        float rotationAngle = 0f;
        float crossScale = 0f;
        float crossRotation = 0f;
        float fillProgress = 0f;
        float flashScale = 0f;
        float flashAlpha = 0f;
        Color crossColor;
        bool crossIsRed = false;

        if (t < appearEnd)
        {
            float progress = t / appearEnd;
            markerScale = EaseOut(progress);
            markerAlpha = EaseOut(progress);
        }
        else if (t < pause1End)
        {
            markerScale = 1f;
            markerAlpha = 1f;
        }
        else if (t < rotateEnd)
        {
            markerScale = 1f;
            markerAlpha = 1f;
            float progress = (t - pause1End) / (rotateEnd - pause1End);
            rotationAngle = MathHelper.DegreesToRadians(45f) * EaseOut(progress);
        }
        else if (t < crossEnd)
        {
            markerScale = 1f;
            markerAlpha = 1f;
            rotationAngle = MathHelper.DegreesToRadians(45f);
            crossScale = EaseOut((t - rotateEnd) / (crossEnd - rotateEnd));
            crossRotation = _system.CrossTexture != null
                ? MathHelper.DegreesToRadians(45f)
                : 0f;
        }
        else if (t < pause2End)
        {
            markerScale = 1f;
            markerAlpha = 1f;
            rotationAngle = MathHelper.DegreesToRadians(45f);
            crossScale = 1f;
            crossRotation = _system.CrossTexture != null
                ? MathHelper.DegreesToRadians(45f)
                : 0f;
        }
        else if (t < fillEnd)
        {
            markerScale = 1f;
            markerAlpha = 1f;
            rotationAngle = MathHelper.DegreesToRadians(45f);
            crossScale = 1f;
            fillProgress = EaseOut((t - pause2End) / (fillEnd - pause2End));

            if (_system.CrossTexture != null)
                crossRotation = MathHelper.DegreesToRadians(-45f) * (1f - fillProgress);
            else
                crossRotation = MathHelper.DegreesToRadians(-45f) * fillProgress;
        }
        else if (t < flashAppearEnd)
        {
            markerScale = 1f;
            markerAlpha = 1f;
            rotationAngle = MathHelper.DegreesToRadians(45f);
            fillProgress = 1f;
            crossScale = 1f;
            crossRotation = 0f;
            float progress = (t - fillEnd) / flashAppearDuration;
            flashScale = 1.5f - 0.5f * EaseIn(progress);
            flashAlpha = EaseIn(progress);
            crossIsRed = false;
        }
        else if (t < flashHoldEnd)
        {
            markerScale = 1f;
            markerAlpha = 1f;
            rotationAngle = MathHelper.DegreesToRadians(45f);
            fillProgress = 1f;
            crossScale = 1f;
            crossRotation = 0f;
            flashScale = 1.0f;
            flashAlpha = 1.0f;
            crossIsRed = true;
        }
        else if (t < flashEnd)
        {
            markerScale = 1f;
            markerAlpha = 1f;
            rotationAngle = MathHelper.DegreesToRadians(45f);
            fillProgress = 1f;
            crossScale = 1f;
            crossRotation = 0f;
            float progress = (t - flashHoldEnd) / flashDisappearDuration;
            flashScale = 1.0f;
            flashAlpha = 1f - EaseOut(progress);
            crossIsRed = true;
        }
        else
        {
            markerScale = 1f;
            markerAlpha = 1f;
            rotationAngle = MathHelper.DegreesToRadians(45f);
            fillProgress = 1f;
            crossScale = 1f;
            crossRotation = 0f;
            flashScale = 0f;
            flashAlpha = 0f;
            crossIsRed = true;
        }

        Color stickColor = _system.TargetHighlightColor;
        crossColor = crossIsRed ? _system.TargetHighlightColor : _system.TargetMarkerCrossColor;

        var time = (float)_timing.RealTime.TotalSeconds;
        var jitter = new Vector2(MathF.Sin(time * 25f) * 0.5f, MathF.Cos(time * 20f) * 0.4f);

        DrawTargetMarkerLayer(handle, targetPos + jitter * 0.3f + new Vector2(-1.5f, 0), r, rotationAngle, fillProgress, crossScale, crossRotation, markerAlpha, markerScale, new Color(1f, 0f, 0f, 0.3f), new Color(1f, 0f, 0f, 0.3f), flashAlpha, flashScale);
        DrawTargetMarkerLayer(handle, targetPos + jitter * 0.5f + new Vector2(1.5f, 0), r, rotationAngle, fillProgress, crossScale, crossRotation, markerAlpha, markerScale, new Color(0f, 0.5f, 1f, 0.3f), new Color(0f, 0.5f, 1f, 0.3f), flashAlpha, flashScale);
        DrawTargetMarkerLayer(handle, targetPos, r, rotationAngle, fillProgress, crossScale, crossRotation, markerAlpha, markerScale, stickColor, crossColor, flashAlpha, flashScale);
    }

    private void DrawTargetMarkerLayer(
        DrawingHandleScreen handle,
        Vector2 targetPos,
        float r,
        float rotationAngle,
        float fillProgress,
        float crossScale,
        float crossRotation,
        float markerAlpha,
        float markerScale,
        Color stickColor,
        Color crossColor,
        float flashAlpha,
        float flashScale)
    {
        float baseRadius = r / MathF.Sqrt(2f);
        float stickOffset = baseRadius * markerScale * (1f - fillProgress * 0.3f);
        Vector2[] stickPositions = new Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            float angle = rotationAngle + i * MathF.PI / 2f;
            stickPositions[i] = targetPos + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * stickOffset;
        }

        if (_system.CornerStickTexture != null)
        {
            DrawStickCorners(handle, stickPositions, rotationAngle, stickColor, markerAlpha);
        }
        else
        {
            DrawFallbackCorners(handle, stickPositions, rotationAngle, stickColor, markerAlpha);
        }

        if (crossScale > 0f)
        {
            float crossSize = r * 0.5f * crossScale;
            if (_system.CrossTexture != null)
            {
                var size = _system.CrossTexture.Size * crossScale;
                var transform = Matrix3x2.CreateRotation(crossRotation, targetPos);
                handle.SetTransform(transform);
                handle.DrawTextureRect(_system.CrossTexture, UIBox2.FromDimensions(targetPos - size / 2, size), crossColor);
                handle.SetTransform(Matrix3x2.Identity);
            }
            else
            {
                Vector2 offset = new Vector2(crossSize / 2f);
                var transform = Matrix3x2.CreateRotation(crossRotation, targetPos);
                handle.SetTransform(transform);
                DrawThickLine(handle, targetPos - offset, targetPos + offset, crossColor, 2f);
                DrawThickLine(handle, targetPos - new Vector2(-offset.X, offset.Y), targetPos + new Vector2(-offset.X, offset.Y), crossColor, 2f);
                handle.SetTransform(Matrix3x2.Identity);
            }
        }

        if (flashAlpha > 0f && flashScale > 0f)
        {
            Vector2[] flashVertices = new Vector2[4];
            float flashHalfDiag = baseRadius * flashScale;
            for (int i = 0; i < 4; i++)
            {
                float angle = i * MathF.PI / 2f;
                flashVertices[i] = targetPos + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * flashHalfDiag;
            }
            DrawFilledRhombus(handle, flashVertices, _system.TargetMarkerCrossColor, flashAlpha);
        }
    }

    private void DrawAmmoCounter(DrawingHandleScreen handle)
    {
        int ammo = _system.AmmoCount;
        string ammoStr = ammo.ToString();
        var digitTextures = _system.AmmoDigitTextures;
        Vector2 startPos = _system.MouseScreenPosition + new Vector2(30, 0);
        var color = _system.AmmoDigitsColor;
        var shadowColor = _system.ShadowColor;
        var shadowOffset = _system.ShadowOffset;
        var time = (float)_timing.RealTime.TotalSeconds;

        if (!_isGlitching && _timing.RealTime >= _nextGlitchTime)
        {
            _isGlitching = true;
            _glitchEndTime = _timing.RealTime + TimeSpan.FromSeconds(_system.GlitchDuration);
        }
        else if (_isGlitching && _timing.RealTime >= _glitchEndTime)
        {
            _isGlitching = false;
            _nextGlitchTime = _timing.RealTime + TimeSpan.FromSeconds(_random.NextFloat(_system.GlitchMinInterval, _system.GlitchMaxInterval));
        }

        float spacing = 0f;
        foreach (char c in ammoStr)
        {
            int digit = c - '0';
            if (digit < 0 || digit > 9 || digitTextures[digit] == null)
                continue;

            var tex = digitTextures[digit]!;
            var size = tex.Size * _system.AmmoDigitsScale;
            Vector2 basePos = startPos + new Vector2(spacing, -size.Y / 2f);

            handle.DrawTextureRect(tex, UIBox2.FromDimensions(basePos + shadowOffset, size), shadowColor);

            if (_system.AmmoHoloEffect)
            {
                float jitterX = MathF.Sin(time * 40f + digit * 1.7f) * 0.6f;
                float jitterY = MathF.Cos(time * 35f + digit * 2.3f) * 0.4f;
                Vector2 jitter = new(jitterX, jitterY);

                Vector2 redOffset = new Vector2(-2f, 0f) + jitter * 0.5f;
                handle.DrawTextureRect(tex, UIBox2.FromDimensions(basePos + redOffset, size), new Color(1f, 0f, 0f, 0.35f));

                Vector2 blueOffset = new Vector2(2f, 0f) + jitter * 0.7f;
                handle.DrawTextureRect(tex, UIBox2.FromDimensions(basePos + blueOffset, size), new Color(0f, 0.5f, 1f, 0.35f));

                handle.DrawTextureRect(tex, UIBox2.FromDimensions(basePos + jitter, size), new Color(0f, 1f, 1f, 0.15f));
            }

            float pulse = 0.9f + 0.1f * MathF.Sin(time * 6f);
            var mainColor = color.WithAlpha(color.A * pulse);
            handle.DrawTextureRect(tex, UIBox2.FromDimensions(basePos, size), mainColor);

            if (_isGlitching)
            {
                for (int g = 0; g < 4; g++)
                {
                    float fragW = size.X * _random.NextFloat(0.2f, 0.5f);
                    float fragH = size.Y * _random.NextFloat(0.15f, 0.4f);
                    float fragX = basePos.X + _random.NextFloat(0f, size.X - fragW);
                    float fragY = basePos.Y + _random.NextFloat(0f, size.Y - fragH);

                    float shiftX = _random.NextFloat(-_system.GlitchStrength, _system.GlitchStrength);
                    float shiftY = _random.NextFloat(-_system.GlitchStrength, _system.GlitchStrength) * 0.5f;

                    var fragPos = new Vector2(fragX + shiftX, fragY + shiftY);
                    var fragRect = UIBox2.FromDimensions(fragPos, new Vector2(fragW, fragH));

                    handle.DrawRect(fragRect, new Color(0.2f, 0.9f, 1f, 0.25f));
                }
            }

            spacing += size.X + 2f;
        }
    }

    private void DrawStickCorners(DrawingHandleScreen handle, Vector2[] positions, float baseAngle, Color color, float alpha)
    {
        var texture = _system.CornerStickTexture;
        if (texture == null)
            return;

        for (int i = 0; i < positions.Length; i++)
        {
            float angle = baseAngle + i * MathF.PI / 2f;
            float tangentAngle = angle + MathF.PI / 2f;
            var size = texture.Size;
            var transform = Matrix3x2.CreateRotation(tangentAngle, positions[i]);
            handle.SetTransform(transform);
            handle.DrawTextureRect(texture, UIBox2.FromDimensions(positions[i] - size / 2, size), color.WithAlpha(color.A * alpha));
            handle.SetTransform(Matrix3x2.Identity);
        }
    }

    private void DrawFallbackCorners(DrawingHandleScreen handle, Vector2[] positions, float baseAngle, Color color, float alpha)
    {
        float stickLength = 8f;
        for (int i = 0; i < positions.Length; i++)
        {
            float angle = baseAngle + i * MathF.PI / 2f;
            Vector2 radial = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            Vector2 tangent = new Vector2(-radial.Y, radial.X);
            Vector2 p = positions[i];
            DrawThickLine(handle, p - tangent * stickLength, p + tangent * stickLength, color.WithAlpha(color.A * alpha), 2f);
        }
    }

    private void DrawThickLine(DrawingHandleScreen handle, Vector2 from, Vector2 to, Color color, float thickness)
    {
        Vector2 dir = to - from;
        float len = dir.Length();
        if (len < 0.01f)
            return;
        Vector2 normal = new Vector2(-dir.Y, dir.X) / len;
        int steps = Math.Max(1, (int)MathF.Ceiling(thickness));
        float half = thickness / 2f;
        for (float offset = -half; offset <= half; offset += 1f)
        {
            handle.DrawLine(from + normal * offset, to + normal * offset, color);
        }
    }

    private void DrawFilledRhombus(DrawingHandleScreen handle, Vector2[] vertices, Color color, float alpha)
    {
        if (alpha <= 0f || vertices.Length != 4)
            return;

        Color fillColor = color.WithAlpha(color.A * alpha);
        float minY = vertices[0].Y, maxY = vertices[0].Y;
        for (int i = 1; i < vertices.Length; i++)
        {
            if (vertices[i].Y < minY) minY = vertices[i].Y;
            if (vertices[i].Y > maxY) maxY = vertices[i].Y;
        }

        for (float y = minY; y <= maxY; y += 1f)
        {
            var intersections = new System.Collections.Generic.List<float>();
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector2 p1 = vertices[i];
                Vector2 p2 = vertices[(i + 1) % vertices.Length];
                if ((p1.Y <= y && p2.Y > y) || (p2.Y <= y && p1.Y > y))
                {
                    float t = (y - p1.Y) / (p2.Y - p1.Y);
                    float x = p1.X + t * (p2.X - p1.X);
                    intersections.Add(x);
                }
            }
            intersections.Sort();
            for (int i = 0; i < intersections.Count - 1; i += 2)
            {
                handle.DrawLine(new Vector2(intersections[i], y), new Vector2(intersections[i + 1], y), fillColor);
            }
        }
    }

    private float EaseOut(float x) => 1f - MathF.Pow(1f - x, 3f);
    private float EaseIn(float x) => x * x * x;
}