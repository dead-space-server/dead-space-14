// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Wizard.Runes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.Wizard.Runes;

public sealed class RuneSigilOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";
    private static readonly ProtoId<ShaderPrototype> GlowShader = "RuneAdditive";
    private static readonly Color Night = Color.FromHex("#0b0614");
    private static readonly Color Danger = Color.FromHex("#ff3b4a");

    private const float BaseRadius = 0.78f;
    private const float RadiusPerRune = 0.05f;
    private const float GlyphDraw = 0.35f;
    private const float PixelsPerMeter = 32f;

    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private static readonly SpriteSpecifier Rock = new SpriteSpecifier.Rsi(new ResPath("/Textures/Objects/Weapons/Guns/Projectiles/meteor.rsi"), "large");

    private readonly RuneVisualsSystem _system;
    private readonly TransformSystem _transform;
    private readonly SpriteSystem _sprite;
    private readonly ShaderInstance _unshaded;
    private readonly ShaderInstance _glow;
    private readonly List<Vector2> _quad = new(6);
    private readonly List<RunePrototype> _runes = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities | OverlaySpace.WorldSpace;

    public RuneSigilOverlay(RuneVisualsSystem system)
    {
        IoCManager.InjectDependencies(this);
        _system = system;
        _transform = _entity.System<TransformSystem>();
        _sprite = _entity.System<SpriteSystem>();
        _unshaded = _proto.Index(UnshadedShader).Instance();
        _glow = _proto.Index(GlowShader).Instance();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var eye = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        if (args.Space == OverlaySpace.WorldSpaceBelowEntities)
        {
            DrawAuras(handle, args, eye, false);
            if (_system.Tracks.Count > 0)
                DrawSigils(handle, args, eye);
        }
        else
        {
            DrawAuras(handle, args, eye, true);
            if (_system.Impacts.Count > 0)
                DrawImpacts(handle, args, eye);
            if (_system.Aim is { } aim)
                DrawAim(handle, args, eye, aim);
        }

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawAuras(DrawingHandleWorld handle, in OverlayDrawArgs args, Angle eye, bool front)
    {
        var bounds = args.WorldAABB.Enlarged(2f);
        var now = _system.Now;
        var query = _entity.EntityQueryEnumerator<RuneAuraComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var aura, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (_entity.TryGetComponent(uid, out SpriteComponent? sprite) && !sprite.Visible)
                continue;

            var position = _transform.GetWorldPosition(xform);
            if (!bounds.Contains(position))
                continue;

            handle.SetTransform(Matrix3x2.Multiply(Matrix3Helpers.CreateRotation(-eye), Matrix3Helpers.CreateTranslation(position)));
            var seed = uid.Id % 97 * 0.37f;
            if (aura.Shields > 0)
                DrawShields(handle, aura.Shields, now + seed, front);

            foreach (var charm in aura.Charms)
                DrawCharm(handle, charm, now, seed, front);
        }
    }

    private void DrawShields(DrawingHandleWorld handle, int count, float now, bool front)
    {
        var rock = _sprite.Frame0(Rock);
        for (var i = 0; i < count; i++)
        {
            var angle = now * 1.4f + i * MathF.Tau / count;
            var depth = MathF.Sin(angle);
            if (depth < 0f != front)
                continue;

            var at = new Vector2(MathF.Cos(angle) * 0.62f, depth * 0.24f - 0.08f);
            var size = 0.3f * (1f - 0.18f * depth);
            handle.UseShader(_glow);
            Texture(handle, RuneCircleArt.Glow(_clyde), at, size * 2.2f, 0f, Color.FromHex("#c9b59a").WithAlpha(0.25f));
            handle.UseShader(null);
            handle.DrawTextureRect(rock, new Box2Rotated(Box2.CenteredAround(at, new Vector2(size)), new Angle(angle * 0.5f), at));
        }
    }

    private void DrawCharm(DrawingHandleWorld handle, string charm, float now, float seed, bool front)
    {
        handle.UseShader(_glow);
        switch (charm)
        {
            case "RuneAlertPhoenix":
                if (!front)
                {
                    Texture(handle, RuneCircleArt.Glow(_clyde), new Vector2(0f, -0.05f), 1.5f, 0f, Color.FromHex("#ff7a2e").WithAlpha(0.22f + 0.06f * MathF.Sin(now * 9f)));
                    break;
                }

                for (var i = 0; i < 7; i++)
                {
                    var life = (now * 0.7f + i / 7f + seed) % 1f;
                    var x = MathF.Sin(i * 2.3f + seed) * 0.32f + MathF.Sin(now * 3f + i) * 0.04f;
                    var at = new Vector2(x, -0.4f + life * 0.95f);
                    var ember = Color.InterpolateBetween(Color.FromHex("#ffe9a0"), Color.FromHex("#e03a12"), life);
                    Texture(handle, RuneCircleArt.Glow(_clyde), at, 0.16f * (1f - life * 0.5f), 0f, ember.WithAlpha(MathF.Sin(life * MathF.PI)));
                }
                break;

            case "RuneAlertIceArmor":
                if (!front)
                    break;

                TextureBox(handle, RuneCircleArt.Ring(_clyde, 10f), new Vector2(0f, -0.02f), new Vector2(0.95f, 1.2f), Color.FromHex("#9be7ff").WithAlpha(0.55f));
                TextureBox(handle, RuneCircleArt.Glow(_clyde), new Vector2(0f, -0.02f), new Vector2(1.2f, 1.5f), Color.FromHex("#9be7ff").WithAlpha(0.12f));
                for (var i = 0; i < 4; i++)
                {
                    var angle = i * MathF.Tau / 4f + seed;
                    var twinkle = MathF.Max(0f, MathF.Sin(now * 3f + i * 1.7f));
                    var at = new Vector2(MathF.Cos(angle) * 0.47f, MathF.Sin(angle) * 0.6f - 0.02f);
                    Texture(handle, RuneCircleArt.Glow(_clyde), at, 0.14f, 0f, Color.White.WithAlpha(twinkle));
                }
                break;

            case "RuneAlertStoneSkin":
                if (front)
                {
                    for (var i = 0; i < 5; i++)
                    {
                        var angle = now * 0.6f + i * MathF.Tau / 5f + seed;
                        var at = new Vector2(MathF.Cos(angle) * 0.38f, MathF.Sin(angle) * 0.5f);
                        Texture(handle, RuneCircleArt.Glow(_clyde), at, 0.12f, 0f, Color.FromHex("#c9b59a").WithAlpha(0.7f));
                    }
                    break;
                }

                TextureBox(handle, RuneCircleArt.Ring(_clyde, 18f), new Vector2(0f, -0.42f), new Vector2(0.9f, 0.32f), Color.FromHex("#b58a5a").WithAlpha(0.6f));
                break;

            case "RuneAlertLightningRod":
            {
                if (!front)
                    break;

                var jitter = new Random((int) (now * 14f) * 17 + (int) (seed * 100f));
                var flicker = jitter.NextSingle() > 0.3f ? 1f : 0.3f;
                for (var arc = 0; arc < 2; arc++)
                {
                    var angle = jitter.NextSingle() * MathF.Tau;
                    var previous = new Vector2(MathF.Cos(angle) * 0.3f, MathF.Sin(angle) * 0.45f);
                    for (var step = 0; step < 3; step++)
                    {
                        var next = previous + new Vector2(jitter.NextSingle() - 0.5f, jitter.NextSingle() - 0.5f) * 0.22f;
                        Line(handle, previous, next, 0.025f, Color.FromHex("#fff79a").WithAlpha(flicker));
                        previous = next;
                    }
                }
                Texture(handle, RuneCircleArt.Glow(_clyde), Vector2.Zero, 1.1f, 0f, Color.FromHex("#fff36b").WithAlpha(0.1f * flicker));
                break;
            }

            case "RuneAlertWaterBubble":
                if (!front)
                    break;

                Texture(handle, RuneCircleArt.Glow(_clyde), new Vector2(0f, 0.22f), 0.75f, 0f, Color.FromHex("#63b7e6").WithAlpha(0.18f));
                Texture(handle, RuneCircleArt.Ring(_clyde, 14f), new Vector2(0f, 0.22f), 0.62f, 0f, Color.FromHex("#b8ecff").WithAlpha(0.7f));
                Texture(handle, RuneCircleArt.Glow(_clyde), new Vector2(-0.12f, 0.36f), 0.14f, 0f, Color.White.WithAlpha(0.8f));
                break;

            case "RuneAlertRegeneration":
                if (!front)
                    break;

                for (var i = 0; i < 5; i++)
                {
                    var life = (now * 0.5f + i / 5f + seed) % 1f;
                    var at = new Vector2(MathF.Sin(i * 1.9f + seed) * 0.36f, -0.3f + life * 0.8f);
                    var alpha = MathF.Sin(life * MathF.PI);
                    var green = Color.FromHex("#8fe07a").WithAlpha(alpha);
                    Line(handle, at - new Vector2(0.045f, 0f), at + new Vector2(0.045f, 0f), 0.022f, green);
                    Line(handle, at - new Vector2(0f, 0.045f), at + new Vector2(0f, 0.045f), 0.022f, green);
                }
                break;

            case "RuneAlertViperBlood":
                if (front)
                {
                    for (var i = 0; i < 4; i++)
                    {
                        var life = (now * 0.35f + i / 4f + seed) % 1f;
                        var at = new Vector2(MathF.Sin(i * 2.7f + seed) * 0.3f, -0.42f + life * 0.35f);
                        Texture(handle, RuneCircleArt.Glow(_clyde), at, 0.12f, 0f, Color.FromHex("#b6f25c").WithAlpha(MathF.Sin(life * MathF.PI)));
                    }
                    break;
                }

                TextureBox(handle, RuneCircleArt.Glow(_clyde), new Vector2(0f, -0.4f), new Vector2(1.1f, 0.45f), Color.FromHex("#6cc23b").WithAlpha(0.35f));
                break;

            case "RuneAlertImmortal":
                if (!front)
                {
                    Texture(handle, RuneCircleArt.Glow(_clyde), Vector2.Zero, 1.4f, 0f, Color.FromHex("#ffeb55").WithAlpha(0.18f + 0.05f * MathF.Sin(now * 4f)));
                    break;
                }

                var bob = MathF.Sin(now * 2.5f) * 0.02f;
                TextureBox(handle, RuneCircleArt.Ring(_clyde, 34f), new Vector2(0f, 0.55f + bob), new Vector2(0.38f, 0.13f), Color.FromHex("#ffeb55").WithAlpha(0.95f));
                TextureBox(handle, RuneCircleArt.Glow(_clyde), new Vector2(0f, 0.55f + bob), new Vector2(0.6f, 0.28f), Color.FromHex("#ffeb55").WithAlpha(0.35f));
                break;
        }
    }

    private void DrawSigils(DrawingHandleWorld handle, in OverlayDrawArgs args, Angle eye)
    {
        var bounds = args.WorldAABB.Enlarged(2f);
        var now = _system.Now;
        var query = _entity.EntityQueryEnumerator<RuneSigilComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var sigil, out var xform))
        {
            if (xform.MapID != args.MapId || !_system.Tracks.TryGetValue(uid, out var track))
                continue;

            var position = _transform.GetWorldPosition(xform);
            if (!bounds.Contains(position))
                continue;

            _runes.Clear();
            foreach (var id in sigil.Chain)
            {
                if (_proto.TryIndex<RunePrototype>(id, out var rune))
                    _runes.Add(rune);
            }

            if (_runes.Count == 0)
                continue;

            handle.SetTransform(Matrix3x2.Multiply(Matrix3Helpers.CreateRotation(-eye), Matrix3Helpers.CreateTranslation(position)));
            DrawSigil(handle, sigil, track, now);
        }
    }

    private void DrawSigil(DrawingHandleWorld handle, RuneSigilComponent sigil, RuneSigilTrack track, float now)
    {
        var age = (float) (_timing.CurTime - sigil.Started).TotalSeconds;
        var stateAge = (float) (_timing.CurTime - sigil.Changed).TotalSeconds;
        var stumble = (float) (_timing.CurTime - sigil.Stumbled).TotalSeconds;
        var appear = Ease(Math.Clamp(age / 0.25f, 0f, 1f));

        var alpha = sigil.State switch
        {
            RuneSigilState.Cast => 1f - Math.Clamp((stateAge - 0.15f) / 0.6f, 0f, 1f),
            RuneSigilState.Fizzle => 1f - Math.Clamp(stateAge / 0.9f, 0f, 1f),
            RuneSigilState.Fade => 1f - Math.Clamp(stateAge / 0.6f, 0f, 1f),
            _ => appear,
        };
        if (alpha <= 0.01f)
            return;

        var burst = sigil.State == RuneSigilState.Cast ? Ease(Math.Clamp(stateAge / 0.45f, 0f, 1f)) : 0f;
        var broken = sigil.State == RuneSigilState.Fizzle ? Math.Clamp(stateAge / 0.9f, 0f, 1f) : 0f;
        var shaking = stumble < 0.35f ? 1f - stumble / 0.35f : 0f;

        var radius = (BaseRadius + RadiusPerRune * _runes.Count) * (0.55f + 0.45f * appear) * (1f + 0.35f * burst);
        var center = Vector2.Zero;
        if (shaking > 0f || broken > 0f)
        {
            var amount = 0.04f * shaking + 0.05f * broken * (1f - broken);
            center += new Vector2(MathF.Sin(now * 53f), MathF.Cos(now * 47f)) * amount;
        }

        var form = _runes[0];
        var element = FindKind(RuneKind.Element, 0);
        var second = FindKind(RuneKind.Element, 1);
        var main = element?.Color ?? form.Color;
        if (shaking > 0f)
            main = Color.InterpolateBetween(main, Danger, shaking * 0.7f);
        if (broken > 0f)
            main = Color.InterpolateBetween(main, Danger, MathF.Min(1f, broken * 3f));

        handle.UseShader(_unshaded);
        Texture(handle, RuneCircleArt.Glow(_clyde), center, radius * 2.6f, 0f, Night.WithAlpha(0.5f * alpha));

        handle.UseShader(_glow);
        Texture(handle, RuneCircleArt.Glow(_clyde), center, radius * 3.4f, 0f, main.WithAlpha(0.22f * alpha * (1f + burst)));

        var formAppear = Appear(track, 0, now);
        var style = RuneCircleControl.StyleFor(form.ID);
        var spin = style.Spin * (1f + 3f * burst);
        var formColor = broken > 0f ? Color.InterpolateBetween(form.Color, Danger, MathF.Min(1f, broken * 3f)) : form.Color;
        var ringRadius = radius * (1f + 0.25f * broken);
        Ring(handle, center, ringRadius, 2.4f, style.Dashes, style.Duty, now * spin, formColor.WithAlpha(alpha * formAppear));
        Ring(handle, center, ringRadius * 0.62f, 1f, 0, 1f, 0f, formColor.WithAlpha(0.35f * alpha * formAppear));
        Ring(handle, center, ringRadius * 1.12f, 1f, style.Dashes * 3, 0.3f, -now * spin * 0.5f, formColor.WithAlpha(0.3f * alpha * formAppear));

        if (element != null)
        {
            var elementAppear = Appear(track, 1, now);
            var band = radius * 0.81f;
            var speed = RuneCircleControl.ElementSpeed(element.ID);
            Band(handle, center, band, radius * 0.16f, ElementTint(element, now, 0f).WithAlpha(0.55f * alpha * elementAppear));
            Comet(handle, center, band, radius * 0.15f, now * speed, Color.InterpolateBetween(main, Color.White, 0.5f).WithAlpha(0.8f * alpha * elementAppear));
            Comet(handle, center, band, radius * 0.15f, now * speed + MathF.PI, main.WithAlpha(0.5f * alpha * elementAppear));

            if (second != null)
            {
                var secondAppear = Appear(track, _runes.IndexOf(second), now);
                Band(handle, center, radius * 0.7f, radius * 0.1f, ElementTint(second, now, 0.4f).WithAlpha(0.5f * alpha * secondAppear));
                Comet(handle, center, radius * 0.7f, radius * 0.1f, -now * speed * 1.3f, ElementTint(second, now, 0.4f).WithAlpha(0.8f * alpha * secondAppear));
            }

            for (var i = 0; i < 3; i++)
            {
                var angle = now * 0.25f + i * MathF.Tau / 3f;
                var at = center + Dir(angle) * ringRadius;
                handle.UseShader(_unshaded);
                Texture(handle, RuneCircleArt.Glow(_clyde), at, radius * 0.5f, 0f, Night.WithAlpha(0.6f * alpha * elementAppear));
                handle.UseShader(_glow);
                Glyph(handle, element.Stroke, at, radius * 0.28f, elementAppear, ElementTint(element, now, i * 0.3f).WithAlpha(alpha), 0.035f);
            }
        }

        handle.UseShader(_glow);
        var glyphColor = element != null ? Color.InterpolateBetween(formColor, main, 0.35f) : formColor;
        Glyph(handle, form.Stroke, center, radius * 0.95f, formAppear, glyphColor.WithAlpha(alpha), 0.055f * (1f + burst));

        var modifiers = 0;
        foreach (var rune in _runes)
        {
            if (rune.Kind == RuneKind.Modifier && !rune.Fusion)
                modifiers++;
        }

        var index = 0;
        for (var i = 0; i < _runes.Count; i++)
        {
            var rune = _runes[i];
            if (rune.Kind != RuneKind.Modifier)
                continue;

            var appearMod = Appear(track, i, now);
            if (rune.Fusion)
            {
                Glyph(handle, rune.Stroke, center + new Vector2(0f, -radius * 0.42f), radius * 0.3f, appearMod, Color.White.WithAlpha(0.8f * alpha), 0.03f);
                continue;
            }

            var angle = now * 0.9f + index * MathF.Tau / Math.Max(1, modifiers);
            var orbit = ringRadius * (1.25f + 0.08f * MathF.Sin(now * 3f + index));
            var at = center + Dir(angle) * orbit * (0.6f + 0.4f * appearMod);
            var pulse = rune.Power > 1f ? 0.75f + 0.25f * MathF.Sin(now * 9f) : 1f;
            Texture(handle, RuneCircleArt.Glow(_clyde), at, radius * 0.6f * pulse, 0f, rune.Color.WithAlpha(0.55f * alpha * appearMod));
            Ring(handle, at, radius * 0.17f, 1.2f, 0, 1f, 0f, rune.Color.WithAlpha(alpha * appearMod));
            Glyph(handle, rune.Stroke, at, radius * 0.22f, appearMod, Color.InterpolateBetween(rune.Color, Color.White, 0.4f).WithAlpha(alpha), 0.028f);
            index++;

            if (rune.Power > 1f)
                Texture(handle, RuneCircleArt.Glow(_clyde), center, radius * 1.4f * pulse, 0f, rune.Color.WithAlpha(0.18f * alpha * appearMod));
        }

        if (burst > 0f)
        {
            Ring(handle, center, radius * (1f + 1.6f * burst), 3f, 0, 1f, 0f, Color.InterpolateBetween(main, Color.White, 0.5f).WithAlpha(alpha * (1f - burst)));
            Texture(handle, RuneCircleArt.Glow(_clyde), center, radius * 4f, 0f, main.WithAlpha(0.6f * (1f - burst)));
        }

        if (broken > 0f)
        {
            var shards = 10;
            for (var i = 0; i < shards; i++)
            {
                var angle = i * MathF.Tau / shards + i * 0.37f;
                var from = center + Dir(angle) * radius * (0.9f + broken * 0.9f);
                var to = from + Dir(angle + 1.3f) * radius * 0.25f * (1f - broken);
                Line(handle, from, to, 0.04f, Danger.WithAlpha(alpha));
            }
        }
    }

    private RunePrototype? FindKind(RuneKind kind, int skip)
    {
        foreach (var rune in _runes)
        {
            if (rune.Kind != kind)
                continue;

            if (skip-- == 0)
                return rune;
        }

        return null;
    }

    private static float Appear(RuneSigilTrack track, int index, float now)
    {
        if (index < 0 || index >= track.Appeared.Count)
            return 1f;

        return Ease(Math.Clamp((now - track.Appeared[index]) / GlyphDraw, 0f, 1f));
    }

    private void DrawAim(DrawingHandleWorld handle, in OverlayDrawArgs args, Angle eye, RuneAim aim)
    {
        if (aim.Targeting == RuneTargeting.Self || !_entity.TryGetComponent(aim.Caster, out TransformComponent? xform) || xform.MapID != args.MapId)
            return;

        var now = _system.Now;
        var from = _transform.GetWorldPosition(xform);
        var good = aim.InRange && aim.Valid;
        var marker = good ? aim.Color : Danger;

        handle.UseShader(_glow);
        handle.SetTransform(Matrix3x2.Multiply(Matrix3Helpers.CreateRotation(-eye), Matrix3Helpers.CreateTranslation(from)));
        var dashes = Math.Clamp((int) (aim.Range * 8f), 12, 96);
        Ring(handle, Vector2.Zero, aim.Range, 1.5f, dashes, 0.55f, now * 0.08f, aim.Color.WithAlpha(aim.InRange ? 0.35f : 0.6f));

        if (aim.Point.MapId != args.MapId)
            return;

        handle.SetTransform(Matrix3x2.Identity);
        var to = aim.Point.Position;
        var delta = to - from;
        var length = delta.Length();
        if (length > 0.3f)
        {
            var reach = aim.InRange ? length : MathF.Min(length, aim.Range);
            var direction = delta / length;
            Line(handle, from + direction * 0.3f, from + direction * reach, 0.02f, aim.Color.WithAlpha(0.35f));
            if (!aim.InRange)
                Line(handle, from + direction * reach, to, 0.02f, Danger.WithAlpha(0.45f));
        }

        handle.SetTransform(Matrix3x2.Multiply(Matrix3Helpers.CreateRotation(-eye), Matrix3Helpers.CreateTranslation(to)));
        var pulse = 0.9f + 0.1f * MathF.Sin(now * 6f);
        Ring(handle, Vector2.Zero, 0.26f * pulse, 1.6f, 4, 0.6f, now * 0.8f, marker.WithAlpha(0.9f));
        Texture(handle, RuneCircleArt.Glow(_clyde), Vector2.Zero, 0.35f, 0f, marker.WithAlpha(0.3f));
    }

    private void DrawImpacts(DrawingHandleWorld handle, in OverlayDrawArgs args, Angle eye)
    {
        var bounds = args.WorldAABB.Enlarged(4f);
        var now = _system.Now;
        foreach (var impact in _system.Impacts)
        {
            if (impact.At.MapId != args.MapId || !bounds.Contains(impact.At.Position))
                continue;

            var t = Math.Clamp((now - impact.Start) / RuneVisualsSystem.ImpactLength, 0f, 1f);
            handle.SetTransform(Matrix3x2.Multiply(Matrix3Helpers.CreateRotation(-eye), Matrix3Helpers.CreateTranslation(impact.At.Position)));
            DrawImpact(handle, impact.Element, impact, t, now, 1f);
            if (impact.Second != null)
                DrawImpact(handle, impact.Second, impact, t, now, 0.5f);
        }
    }

    private void DrawImpact(DrawingHandleWorld handle, string elementId, RuneImpact impact, float t, float now, float share)
    {
        if (!_proto.TryIndex<RunePrototype>(elementId, out var element))
            return;

        var random = new Random(impact.Seed * 7919 + elementId.Length * 31);
        var r = MathF.Max(0.5f, impact.Radius);
        var strong = impact.Strong ? 1.4f : 1f;
        var fade = 1f - t;
        var color = element.Color;
        var light = Color.InterpolateBetween(color, Color.White, 0.55f);
        var count = (int) (14 * share * strong);

        handle.UseShader(_glow);
        if (share >= 1f)
        {
            Texture(handle, RuneCircleArt.Glow(_clyde), Vector2.Zero, r * 2.8f * (0.6f + 0.4f * t) * strong, 0f, color.WithAlpha(0.45f * fade * fade));
            Ring(handle, Vector2.Zero, r * (0.25f + 0.85f * Ease(t)), 2f, 0, 1f, 0f, light.WithAlpha(0.7f * fade));
        }

        switch (element.ID)
        {
            case RuneMarkRules.Fire:
                for (var i = 0; i < count; i++)
                {
                    var angle = random.NextSingle() * MathF.Tau;
                    var speed = 0.4f + random.NextSingle() * 0.6f;
                    var at = Dir(angle) * r * speed * Ease(t) + new Vector2(0f, t * (0.4f + random.NextSingle() * 0.5f));
                    var ember = Color.InterpolateBetween(Color.FromHex("#ffe9a0"), Color.FromHex("#e03a12"), t);
                    Texture(handle, RuneCircleArt.Glow(_clyde), at, 0.22f * (1f - t * 0.6f), 0f, ember.WithAlpha(fade));
                }
                break;

            case RuneMarkRules.Ice:
                for (var i = 0; i < 8 * share * strong; i++)
                {
                    var angle = i * MathF.Tau / 8f + random.NextSingle() * 0.3f;
                    var from = Dir(angle) * r * (0.15f + 0.7f * Ease(t));
                    Line(handle, from, from + Dir(angle) * r * 0.35f, 0.05f, light.WithAlpha(fade));
                    Line(handle, from + Dir(angle) * r * 0.12f, from + Dir(angle + 0.6f) * r * 0.12f + Dir(angle) * r * 0.12f, 0.03f, light.WithAlpha(fade * 0.8f));
                }
                Motes(handle, random, count / 2, r, t, Color.White, 0.1f, 0.1f);
                break;

            case RuneMarkRules.Earth:
                handle.UseShader(_unshaded);
                for (var i = 0; i < count; i++)
                {
                    var angle = random.NextSingle() * MathF.Tau;
                    var at = Dir(angle) * r * (0.3f + 0.7f * random.NextSingle()) * Ease(t) + new Vector2(0f, 0.25f * t - 0.6f * t * t);
                    var size = 0.06f + random.NextSingle() * 0.07f;
                    var clod = Color.InterpolateBetween(Color.FromHex("#a9742e"), Color.FromHex("#4a312c"), random.NextSingle());
                    handle.DrawRect(new Box2Rotated(Box2.CenteredAround(at, new Vector2(size)), new Angle(now * 3f + i), at), clod.WithAlpha(fade));
                }
                handle.UseShader(_glow);
                Ring(handle, Vector2.Zero, r * (0.5f + 0.6f * t), 6f, 0, 1f, 0f, Color.FromHex("#b58a5a").WithAlpha(0.35f * fade));
                break;

            case RuneMarkRules.Lightning:
            {
                var flicker = (int) (now * 18f) % 2 == 0 ? 1f : 0.45f;
                var jitter = new Random(impact.Seed * 31 + (int) (now * 16f));
                for (var i = 0; i < 6 * share * strong; i++)
                {
                    var angle = i * MathF.Tau / 6f + jitter.NextSingle() * 0.5f;
                    var previous = Vector2.Zero;
                    for (var step = 1; step <= 4; step++)
                    {
                        var next = Dir(angle + (jitter.NextSingle() - 0.5f) * 0.6f) * r * step / 4f;
                        Line(handle, previous, next, 0.04f, light.WithAlpha(fade * flicker));
                        previous = next;
                    }
                }
                break;
            }

            case RuneMarkRules.Life:
                Motes(handle, random, count, r * 0.8f, t, light, 0.16f, 0.9f);
                break;

            case RuneMarkRules.Void:
                handle.UseShader(_unshaded);
                Texture(handle, RuneCircleArt.Glow(_clyde), Vector2.Zero, r * 2.2f * (1f - 0.5f * t), 0f, Night.WithAlpha(0.7f * fade));
                handle.UseShader(_glow);
                Ring(handle, Vector2.Zero, r * (1.2f - 1.1f * Ease(t)), 2.5f, 0, 1f, 0f, color.WithAlpha(fade));
                for (var i = 0; i < count; i++)
                {
                    var angle = random.NextSingle() * MathF.Tau + t * 2f;
                    var at = Dir(angle) * r * (1.2f - Ease(t)) * (0.6f + 0.4f * random.NextSingle());
                    Texture(handle, RuneCircleArt.Glow(_clyde), at, 0.16f, 0f, light.WithAlpha(fade));
                }
                break;

            case RuneMarkRules.Chaos:
                handle.UseShader(_unshaded);
                for (var i = 0; i < count * 2; i++)
                {
                    var angle = random.NextSingle() * MathF.Tau;
                    var at = Dir(angle) * r * (0.2f + 0.9f * random.NextSingle()) * Ease(t) + new Vector2(0f, -0.3f * t * t);
                    var hue = (random.NextSingle() + now * 0.5f) % 1f;
                    var confetti = Color.FromHsv(new Vector4(hue, 0.8f, 1f, 1f));
                    var size = new Vector2(0.09f, 0.05f);
                    handle.DrawRect(new Box2Rotated(Box2.CenteredAround(at, size), new Angle(now * (4f + i % 5) + i), at), confetti.WithAlpha(fade));
                }
                break;

            case RuneMarkRules.Water:
                Ring(handle, Vector2.Zero, r * (0.3f + 0.8f * Ease(t)), 5f, 0, 1f, 0f, color.WithAlpha(0.5f * fade));
                for (var i = 0; i < count; i++)
                {
                    var angle = random.NextSingle() * MathF.Tau;
                    var at = Dir(angle) * r * (0.4f + 0.6f * random.NextSingle()) * Ease(t) + new Vector2(0f, 0.5f * t - 0.9f * t * t);
                    Texture(handle, RuneCircleArt.Glow(_clyde), at, 0.14f, 0f, light.WithAlpha(fade));
                }
                break;

            case RuneMarkRules.Air:
                for (var i = 0; i < 6 * share * strong; i++)
                {
                    var start = i * MathF.Tau / 6f + t * 4f;
                    var reach = r * (0.3f + 0.8f * Ease(t));
                    var previous = Dir(start) * reach * 0.6f;
                    for (var step = 1; step <= 6; step++)
                    {
                        var angle = start + step * 0.22f;
                        var next = Dir(angle) * reach * (0.6f + step * 0.07f);
                        Line(handle, previous, next, 0.045f * (1f - step / 8f), light.WithAlpha(fade * (1f - step / 7f)));
                        previous = next;
                    }
                }
                Motes(handle, random, count / 2, r, t, Color.White, 0.09f, -0.2f);
                break;

            case RuneMarkRules.Blood:
                handle.UseShader(_unshaded);
                for (var i = 0; i < count; i++)
                {
                    var angle = random.NextSingle() * MathF.Tau;
                    var at = Dir(angle) * r * (0.3f + 0.7f * random.NextSingle()) * Ease(t) + new Vector2(0f, 0.2f * t - 0.8f * t * t);
                    var size = 0.05f + random.NextSingle() * 0.06f;
                    handle.DrawRect(Box2.CenteredAround(at, new Vector2(size, size * 1.4f)), Color.FromHex("#a4161f").WithAlpha(fade));
                }
                break;

            case RuneMarkRules.Death:
                handle.UseShader(_unshaded);
                for (var i = 0; i < 8 * share * strong; i++)
                {
                    var angle = random.NextSingle() * MathF.Tau;
                    var at = Dir(angle) * r * 0.6f * random.NextSingle() + new Vector2(0f, t * 0.7f);
                    var size = r * (0.5f + 0.6f * t) * (0.6f + 0.4f * random.NextSingle());
                    Texture(handle, RuneCircleArt.Glow(_clyde), at, size, 0f, Color.FromHex("#1c0f24").WithAlpha(0.65f * fade));
                }
                handle.UseShader(_glow);
                Ring(handle, Vector2.Zero, r * (0.4f + 0.7f * Ease(t)), 2f, 12, 0.5f, t, Color.FromHex("#8a4bd6").WithAlpha(fade));
                Motes(handle, random, count, r * 0.7f, t, light, 0.1f, 0.7f);
                break;

            case RuneMarkRules.Poison:
                for (var i = 0; i < count; i++)
                {
                    var at = new Vector2((random.NextSingle() - 0.5f) * r * 1.6f, (random.NextSingle() - 0.6f) * r + t * 0.8f * (0.5f + random.NextSingle()));
                    var size = 0.12f + 0.1f * random.NextSingle();
                    var pop = t > 0.75f + random.NextSingle() * 0.2f ? 0f : 1f;
                    Ring(handle, at, size, 1.2f, 0, 1f, 0f, light.WithAlpha(fade * pop));
                }
                break;

            default:
                Motes(handle, random, count, r, t, light, 0.14f, 0.3f);
                break;
        }
    }

    private void Motes(DrawingHandleWorld handle, Random random, int count, float radius, float t, Color color, float size, float rise)
    {
        for (var i = 0; i < count; i++)
        {
            var angle = random.NextSingle() * MathF.Tau;
            var at = Dir(angle) * radius * (0.2f + 0.8f * random.NextSingle()) * (0.4f + 0.6f * Ease(t)) + new Vector2(0f, rise * t);
            Texture(handle, RuneCircleArt.Glow(_clyde), at, size * (1f + random.NextSingle()), 0f, color.WithAlpha(1f - t));
        }
    }

    private Color ElementTint(RunePrototype element, float now, float offset)
    {
        if (element.ID != RuneMarkRules.Chaos)
            return element.Color;

        return Color.FromHsv(new Vector4((now * 0.15f + offset) % 1f, 0.75f, 1f, 1f));
    }

    private void TextureBox(DrawingHandleWorld handle, Texture texture, Vector2 center, Vector2 size, Color color)
    {
        if (color.A <= 0.004f)
            return;

        handle.DrawTextureRect(texture, Box2.CenteredAround(center, size), color);
    }

    private void Texture(DrawingHandleWorld handle, Texture texture, Vector2 center, float diameter, float rotation, Color color)
    {
        if (color.A <= 0.004f || diameter <= 0.01f)
            return;

        var box = Box2.CenteredAround(center, new Vector2(diameter));
        handle.DrawTextureRect(texture, new Box2Rotated(box, new Angle(rotation), center), color);
    }

    private void Ring(DrawingHandleWorld handle, Vector2 center, float radius, float pixels, int dashes, float duty, float rotation, Color color)
    {
        var diameter = RuneCircleArt.RingDiameter(radius);
        if (diameter <= 0.01f)
            return;

        var thickness = Math.Clamp(pixels * RuneCircleArt.RingSize / (diameter * PixelsPerMeter), 2f, 128f);
        thickness = MathF.Pow(2f, MathF.Round(MathF.Log2(thickness) * 2f) / 2f);
        Texture(handle, RuneCircleArt.Ring(_clyde, thickness, dashes, duty), center, diameter, rotation, color);
    }

    private void Band(DrawingHandleWorld handle, Vector2 center, float radius, float width, Color color)
    {
        var diameter = RuneCircleArt.RingDiameter(radius);
        Texture(handle, RuneCircleArt.Band(_clyde, width * RuneCircleArt.RingSize / diameter), center, diameter, 0f, color);
    }

    private void Comet(DrawingHandleWorld handle, Vector2 center, float radius, float width, float rotation, Color color)
    {
        var diameter = RuneCircleArt.RingDiameter(radius);
        Texture(handle, RuneCircleArt.Comet(_clyde, width * RuneCircleArt.RingSize / diameter), center, diameter, rotation, color);
    }

    private void Glyph(DrawingHandleWorld handle, IReadOnlyList<Vector2> stroke, Vector2 center, float size, float progress, Color color, float width)
    {
        if (stroke.Count < 2 || progress <= 0f || color.A <= 0.004f)
            return;

        var total = 0f;
        for (var i = 1; i < stroke.Count; i++)
            total += (stroke[i] - stroke[i - 1]).Length();

        var budget = total * progress;
        var previous = Map(stroke[0], center, size);
        for (var i = 1; i < stroke.Count && budget > 0f; i++)
        {
            var length = (stroke[i] - stroke[i - 1]).Length();
            var part = length <= 0f ? 1f : MathF.Min(1f, budget / length);
            budget -= length;
            var next = Map(Vector2.Lerp(stroke[i - 1], stroke[i], part), center, size);
            Line(handle, previous, next, width * 2.4f, color.WithAlpha(color.A * 0.25f));
            Line(handle, previous, next, width, color);
            previous = next;
        }

        if (progress < 1f)
            Texture(handle, RuneCircleArt.Glow(_clyde), previous, width * 9f, 0f, Color.White.WithAlpha(color.A));
    }

    private static Vector2 Map(Vector2 point, Vector2 center, float size)
    {
        return center + new Vector2(point.X - 0.5f, 0.5f - point.Y) * size;
    }

    private void Line(DrawingHandleWorld handle, Vector2 a, Vector2 b, float width, Color color)
    {
        var delta = b - a;
        var length = delta.Length();
        if (length < 0.0001f || color.A <= 0.004f)
            return;

        var normal = new Vector2(-delta.Y, delta.X) / length * (width / 2f);
        _quad.Clear();
        _quad.Add(a + normal);
        _quad.Add(a - normal);
        _quad.Add(b + normal);
        _quad.Add(b + normal);
        _quad.Add(a - normal);
        _quad.Add(b - normal);
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _quad, color);
    }

    private static Vector2 Dir(float angle)
    {
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
    }

    private static float Ease(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }
}
