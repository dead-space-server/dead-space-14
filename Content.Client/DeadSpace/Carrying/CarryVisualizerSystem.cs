// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using System.Numerics;
using Content.Client.DeadSpace.SlimeForm;
using Content.Shared.DeadSpace.Carrying;
using Content.Shared.DeadSpace.SlimeForm;
using Content.Shared.Humanoid;
using Content.Shared.Rotation;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client.DeadSpace.Carrying;

public sealed class CarryVisualizerSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SlimeFormVisualizerSystem _slimeForm = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private readonly Dictionary<EntityUid, CarryVisualState> _states = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CarriedComponent, AfterAutoHandleStateEvent>(OnCarriedState);
        SubscribeLocalEvent<CarriedComponent, ComponentShutdown>(OnCarriedShutdown);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<CarriedComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var carried, out var sprite))
        {
            UpdateVisual((uid, carried, sprite));
        }
    }

    private void OnCarriedState(Entity<CarriedComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        UpdateVisual((ent.Owner, ent.Comp, sprite));
    }

    private void OnCarriedShutdown(Entity<CarriedComponent> ent, ref ComponentShutdown args)
    {
        ResetVisual(ent.Owner);
    }

    private void UpdateVisual(Entity<CarriedComponent, SpriteComponent> ent)
    {
        if (ent.Comp1.Carrier is not { } carrier ||
            !TryComp<SpriteComponent>(carrier, out var carrierSprite))
        {
            ResetVisual(ent.Owner);
            return;
        }

        if (!_states.ContainsKey(ent.Owner))
        {
            _states[ent.Owner] = new CarryVisualState(
                ent.Comp2.Offset,
                ent.Comp2.DrawDepth,
                ent.Comp2.Rotation,
                ent.Comp2.EnableDirectionOverride,
                ent.Comp2.DirectionOverride);
            _slimeForm.SetCarriedPose(ent.Owner, true);
        }

        var state = _states[ent.Owner];

        var angle = _transform.GetWorldRotation(carrier) + _eye.CurrentEye.Rotation;
        var direction = angle.GetCardinalDir();
        var isHumanoid = HasComp<HumanoidAppearanceComponent>(ent.Owner);

        var slime = TryComp<SlimeFormComponent>(ent.Owner, out var slimeForm) && slimeForm.IsSlime;
        var offset = CarryOffset(direction, isHumanoid, slime);

        offset = ApplyCarrierScaleToOffset(offset, carrierSprite.Scale);

        var behindCarrier = slime
            ? direction == Direction.North
            : direction is Direction.North or Direction.East;

        var drawDepth = behindCarrier
            ? carrierSprite.DrawDepth - 1
            : carrierSprite.DrawDepth + 1;

        if (isHumanoid)
        {
            var rotation = direction switch
            {
                Direction.North => Angle.FromDegrees(90),
                Direction.East => Angle.FromDegrees(90),
                Direction.West => Angle.FromDegrees(-90),
                Direction.South => Angle.FromDegrees(-90),
                _ => Angle.Zero,
            };

            var directionOverride = direction switch
            {
                Direction.South => Direction.South,
                _ => Direction.South,
            };

            ent.Comp2.EnableDirectionOverride = true;
            ent.Comp2.DirectionOverride = directionOverride;
            _sprite.SetRotation((ent.Owner, ent.Comp2), rotation);
        }

        _sprite.SetOffset((ent.Owner, ent.Comp2), state.Offset + offset);
        _sprite.SetDrawDepth((ent.Owner, ent.Comp2), drawDepth);
    }

    private static Vector2 CarryOffset(Direction direction, bool humanoid, bool slime)
    {
        if (humanoid)
        {
            return direction switch
            {
                Direction.North => new Vector2(-0.02f, 0.02f),
                Direction.South => new Vector2(-0.04f, -0.10f),
                Direction.East => new Vector2(0.04f, -0.10f),
                Direction.West => new Vector2(-0.04f, -0.10f),
                _ => new Vector2(-0.08f, -0.08f),
            };
        }

        if (slime)
        {
            return direction switch
            {
                Direction.North => new Vector2(0.02f, -0.02f),
                Direction.South => new Vector2(0.00f, -0.02f),
                Direction.East => new Vector2(0.02f, -0.02f),
                Direction.West => new Vector2(0.00f, -0.02f),
                _ => new Vector2(0.02f, -0.02f),
            };
        }

        return direction switch
        {
            Direction.North => new Vector2(0.02f, 0.02f),
            Direction.South => new Vector2(0.00f, 0.14f),
            Direction.East => new Vector2(0.08f, -0.10f),
            Direction.West => new Vector2(0.00f, -0.10f),
            _ => new Vector2(0.02f, -0.08f),
        };
    }

    private static Vector2 ApplyCarrierScaleToOffset(Vector2 offset, Vector2 carrierScale)
    {
        var normalizedScale = new Vector2(MathF.Abs(carrierScale.X), MathF.Abs(carrierScale.Y));
        return offset * normalizedScale;
    }

    private void ResetVisual(EntityUid uid)
    {
        if (!_states.Remove(uid, out var state))
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        _sprite.SetOffset((uid, sprite), state.Offset);
        _sprite.SetDrawDepth((uid, sprite), state.DrawDepth);
        _sprite.SetRotation((uid, sprite), GetCurrentRotation(uid, state.Rotation));
        _slimeForm.SetCarriedPose(uid, false);

        sprite.EnableDirectionOverride = state.EnableDirectionOverride;
        sprite.DirectionOverride = state.DirectionOverride;
    }

    private Angle GetCurrentRotation(EntityUid uid, Angle fallback)
    {
        if (!TryComp<RotationVisualsComponent>(uid, out var rotationVisuals))
            return fallback;

        if (!TryComp<AppearanceComponent>(uid, out var appearance) ||
            !_appearance.TryGetData<RotationState>(uid, RotationVisuals.RotationState, out var state, appearance))
        {
            return fallback;
        }

        return state == RotationState.Horizontal
            ? rotationVisuals.HorizontalRotation
            : rotationVisuals.VerticalRotation;
    }

    private sealed class CarryVisualState(
        Vector2 offset,
        int drawDepth,
        Angle rotation,
        bool enableDirectionOverride,
        Direction directionOverride)
    {
        public readonly Vector2 Offset = offset;
        public readonly int DrawDepth = drawDepth;
        public readonly Angle Rotation = rotation;
        public readonly bool EnableDirectionOverride = enableDirectionOverride;
        public readonly Direction DirectionOverride = directionOverride;
    }
}
