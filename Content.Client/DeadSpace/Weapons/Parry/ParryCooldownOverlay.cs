// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Weapons.Parry;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Weapons.Parry;

public sealed class ParryCooldownOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private readonly IEntityManager _entities;
    private readonly IGameTiming _timing;
    private readonly IInputManager _input;
    private readonly IPlayerManager _players;
    private readonly SharedHandsSystem _hands;

    public ParryCooldownOverlay(
        IEntityManager entities,
        IGameTiming timing,
        IInputManager input,
        IPlayerManager players,
        SharedHandsSystem hands)
    {
        _entities = entities;
        _timing = timing;
        _input = input;
        _players = players;
        _hands = hands;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_players.LocalEntity is not { } user ||
            !_hands.TryGetActiveItem(user, out var held) ||
            !_entities.TryGetComponent<ParryComponent>(held, out var parry) ||
            parry.ActiveUntil <= _timing.CurTime)
        {
            return;
        }

        var duration = (parry.ActiveUntil - parry.CooldownStart).TotalSeconds;
        if (duration <= 0)
            return;

        var remaining = (float) Math.Clamp(
            (parry.ActiveUntil - _timing.CurTime).TotalSeconds / duration,
            0d,
            1d);

        var handle = args.ScreenHandle;
        var center = _input.MouseScreenPosition.Position;
        var time = (float) _timing.CurTime.TotalSeconds;
        var pulse = 0.84f + MathF.Sin(time * 10f) * 0.12f;
        var color = Color.FromHex("#7BEAFFFF");

        DrawArcRing(handle, center, 27f, 35f, 0f, MathF.Tau, Color.Black.WithAlpha(0.34f));
        DrawSegmentedRing(handle, center, 29f, 34f, remaining, color.WithAlpha(0.86f * pulse));
        DrawArcRing(handle, center, 35f, 36f, 0f, MathF.Tau, color.WithAlpha(0.18f * pulse));

        var glintAngle = -MathF.PI / 2f + time * 3.5f;
        DrawArcRing(handle, center, 34f, 38f, glintAngle, glintAngle + 0.22f, color.WithAlpha(0.78f));
        handle.DrawCircle(center, 24f, color.WithAlpha(0.06f * pulse));
    }

    private static void DrawSegmentedRing(
        DrawingHandleScreen handle,
        Vector2 center,
        float innerRadius,
        float outerRadius,
        float remaining,
        Color color)
    {
        const int segments = 16;
        const float gap = 0.045f;
        var completed = remaining * segments;

        for (var i = 0; i < segments; i++)
        {
            var fill = Math.Clamp(completed - i, 0f, 1f);
            if (fill <= 0f)
                break;

            var start = -MathF.PI / 2f + MathF.Tau * i / segments + gap;
            var end = -MathF.PI / 2f + MathF.Tau * (i + fill) / segments - gap;
            DrawArcRing(handle, center, innerRadius, outerRadius, start, end, color);
        }
    }

    private static void DrawArcRing(
        DrawingHandleScreen handle,
        Vector2 center,
        float innerRadius,
        float outerRadius,
        float startAngle,
        float endAngle,
        Color color)
    {
        const int resolution = 48;
        var sweep = endAngle - startAngle;
        var segmentCount = Math.Max(1, (int) MathF.Ceiling(resolution * MathF.Abs(sweep) / MathF.Tau));
        var vertices = new Vector2[(segmentCount + 1) * 2];

        for (var i = 0; i <= segmentCount; i++)
        {
            var angle = startAngle + sweep * i / segmentCount;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            vertices[i * 2] = center + direction * outerRadius;
            vertices[i * 2 + 1] = center + direction * innerRadius;
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, vertices, color);
    }
}
