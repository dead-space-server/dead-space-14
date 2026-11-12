using Content.Client.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    private const string CasingFadeAnimationKey = "casing-fade";

    private void InitializeSpentAmmo()
    {
        SubscribeLocalEvent<SpentAmmoVisualsComponent, AppearanceChangeEvent>(OnSpentAmmoAppearance);
        SubscribeLocalEvent<CasingFadeComponent, ComponentStartup>(OnCasingFadeStartup);
    }

    private void OnSpentAmmoAppearance(Entity<SpentAmmoVisualsComponent> ent, ref AppearanceChangeEvent args)
    {
        var sprite = args.Sprite;
        if (sprite == null) return;

        if (!args.TryGetData<bool>(AmmoVisuals.Spent, out var spent))
            return;

        string state;

        if (spent)
            state = ent.Comp.Suffix ? $"{ent.Comp.State}-spent" : "spent";
        else
            state = ent.Comp.State;

        _sprite.LayerSetRsiState((ent, sprite), AmmoVisualLayers.Base, state);
        _sprite.RemoveLayer((ent, sprite), AmmoVisualLayers.Tip, false);
    }

    private void OnCasingFadeStartup(Entity<CasingFadeComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var player = EnsureComp<AnimationPlayerComponent>(ent);
        if (_animPlayer.HasRunningAnimation(player, CasingFadeAnimationKey))
            return;

        var color = sprite.Color.WithAlpha(1f);
        _sprite.SetColor((ent.Owner, sprite), color);
        _animPlayer.Play((ent.Owner, player), GetCasingFadeAnimation(ent.Comp, color), CasingFadeAnimationKey);
    }

    private static Animation GetCasingFadeAnimation(CasingFadeComponent component, Color color)
    {
        var fadeDelay = MathF.Max(component.FadeDelay, 0f);
        var fadeDuration = MathF.Max(component.FadeDuration, 0.01f);

        return new Animation
        {
            Length = TimeSpan.FromSeconds(fadeDelay + fadeDuration),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Color),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(color.WithAlpha(1f), 0f),
                        new AnimationTrackProperty.KeyFrame(color.WithAlpha(1f), fadeDelay),
                        new AnimationTrackProperty.KeyFrame(color.WithAlpha(0f), fadeDuration),
                    }
                }
            }
        };
    }
}
