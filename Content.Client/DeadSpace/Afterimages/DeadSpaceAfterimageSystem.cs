// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using System.Linq;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Spawners;

namespace Content.Client.DeadSpace.Afterimages;

public sealed partial class DeadSpaceAfterimageSystem : EntitySystem
{
    [Dependency] private AnimationPlayerSystem _animation = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const string AnimationKey = "deadspace-afterimage";

    public bool TrySpawnAfterimage(
        EntityUid source,
        EntityCoordinates coordinates,
        Angle rotation,
        Color color,
        float lifetime,
        string fallbackEffect,
        Color? endColor = null)
    {
        if (!coordinates.IsValid(EntityManager))
            return false;

        if (Deleted(source) || !TryComp<SpriteComponent>(source, out var sourceSprite))
            return SpawnFallback(coordinates, fallbackEffect);

        var clone = Spawn("clientsideclone", coordinates);
        _metaData.SetEntityName(clone, MetaData(source).EntityName);

        var sprite = Comp<SpriteComponent>(clone);
        _sprite.CopySprite((source, sourceSprite), (clone, sprite));
        _sprite.SetVisible((clone, sprite), true);
        _sprite.SetColor((clone, sprite), color);
        _transform.SetWorldRotationNoLerp(clone, rotation);

        for (var layerIndex = 0; layerIndex < sprite.AllLayers.Count(); layerIndex++)
        {
            sprite.LayerSetShader(layerIndex, "unshaded");
        }

        var despawn = EnsureComp<TimedDespawnComponent>(clone);
        despawn.Lifetime = lifetime;

        var animationPlayer = EnsureComp<AnimationPlayerComponent>(clone);
        _animation.Play((clone, animationPlayer), GetFadeAnimation(color, lifetime, endColor), AnimationKey);

        return true;
    }

    private bool SpawnFallback(EntityCoordinates coordinates, string fallbackEffect)
    {
        if (string.IsNullOrWhiteSpace(fallbackEffect))
            return false;

        if (!coordinates.IsValid(EntityManager))
            return false;

        Spawn(fallbackEffect, coordinates);
        return true;
    }

    private static Animation GetFadeAnimation(Color color, float lifetime, Color? endColor = null)
    {
        if (endColor == null)
        {
            return new Animation
            {
                Length = TimeSpan.FromSeconds(lifetime),
                AnimationTracks =
                {
                    new AnimationTrackComponentProperty
                    {
                        ComponentType = typeof(SpriteComponent),
                        Property = nameof(SpriteComponent.Color),
                        InterpolationMode = AnimationInterpolationMode.Linear,
                        KeyFrames =
                        {
                            new AnimationTrackProperty.KeyFrame(color, 0f),
                            new AnimationTrackProperty.KeyFrame(color.WithAlpha(0f), lifetime),
                        },
                    },
                },
            };
        }

        var finalColor = endColor.Value;
        return new Animation
        {
            Length = TimeSpan.FromSeconds(lifetime),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Color),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(color, 0f),
                        new AnimationTrackProperty.KeyFrame(finalColor.WithAlpha(finalColor.A * 0.65f), lifetime * 0.55f),
                        new AnimationTrackProperty.KeyFrame(finalColor.WithAlpha(0f), lifetime),
                    },
                },
            },
        };
    }
}
