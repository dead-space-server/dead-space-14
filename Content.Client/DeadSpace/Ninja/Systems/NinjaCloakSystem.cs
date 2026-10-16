// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Ninja.Components;
using Content.Client.DeadSpace.Ninja.Components;
using Content.Shared.DeadSpace.Ninja.Systems;
using Content.Shared.DeadSpace.ThermalVision;
using Robust.Client.GameObjects;
using Robust.Client.Animations;

namespace Content.Client.DeadSpace.Ninja.Systems;

public sealed class NinjaCloakSystem : SharedNinjaCloakSystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly AnimationPlayerSystem _animation = default!;

    private readonly Dictionary<EntityUid, EntityUid> _hidden = new();

    private const string AnimKey = "ninja_reveal";
    private const float AnimLength = 0.4f;
    private const float RevealAlpha = 0.7f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NinjaCloakComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<NinjaCloakComponent, AfterAutoHandleStateEvent>(OnStateChanged);
        SubscribeLocalEvent<NinjaCloakComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = AllEntityQuery<NinjaCloakComponent>();

        while (query.MoveNext(out var suitUid, out var comp))
        {
            if (!comp.Enabled)
                continue;

            ApplyCloaked(suitUid);
        }
    }

    private void OnStartup(Entity<NinjaCloakComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.Enabled)
            ApplyCloaked(ent.Owner);
    }

    private void OnStateChanged(Entity<NinjaCloakComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (args.State is not NinjaCloakComponent.NinjaCloakComponent_AutoState)
            return;

        if (ent.Comp.Enabled)
            ApplyCloaked(ent.Owner);
        else
            Restore(ent.Owner);
    }

    private void OnShutdown(Entity<NinjaCloakComponent> ent, ref ComponentShutdown args)
    {
        Restore(ent.Owner);
    }

    private void ApplyCloaked(EntityUid suitUid)
    {
        var wearer = Transform(suitUid).ParentUid;

        if (!wearer.IsValid() || !TryComp<SpriteComponent>(wearer, out var sprite))
            return;

        if (_hidden.TryGetValue(suitUid, out var previous) && previous != wearer)
            Restore(suitUid);

        _hidden[suitUid] = wearer;

        if (TryComp<NinjaCloakRevealedComponent>(wearer, out var reveal))
        {
            if (reveal.Shown != reveal.Revealed)
            {
                reveal.Shown = reveal.Revealed;
                PlayFade(wearer, sprite, reveal.Revealed);
            }

            var running = _animation.HasRunningAnimation(wearer, AnimKey);

            if (!reveal.Revealed && !running && sprite.Color.A <= 0f)
            {
                _sprite.SetVisible((wearer, sprite), false);
                RemCompDeferred<NinjaCloakRevealedComponent>(wearer);
            }
            else
            {
                _sprite.SetVisible((wearer, sprite), true);
            }
        }
        else
        {
            _sprite.SetVisible((wearer, sprite), false);
        }

        if (TryComp<ThermalVisibleComponent>(wearer, out var thermal))
            thermal.DrawWhenInvisible = true;
    }

    private void PlayFade(EntityUid wearer, SpriteComponent sprite, bool reveal)
    {
        _animation.Stop(wearer, AnimKey);

        var from = reveal && sprite.Color.A >= 0.99f ? 0f : sprite.Color.A;
        var to = reveal ? RevealAlpha : 0f;

        _sprite.SetColor((wearer, sprite), sprite.Color.WithAlpha(from));

        _animation.Play(wearer, new Animation
        {
            Length = TimeSpan.FromSeconds(AnimLength),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Color),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(sprite.Color.WithAlpha(from), 0f),
                        new AnimationTrackProperty.KeyFrame(sprite.Color.WithAlpha(to), AnimLength)
                    }
                }
            }
        }, AnimKey);
    }

    private void Restore(EntityUid suitUid)
    {
        if (!_hidden.Remove(suitUid, out var wearer))
            return;

        if (!TryComp<SpriteComponent>(wearer, out var sprite))
            return;

        _animation.Stop(wearer, AnimKey);
        _sprite.SetVisible((wearer, sprite), true);
        _sprite.SetColor((wearer, sprite), sprite.Color.WithAlpha(1f));
        RemComp<NinjaCloakRevealedComponent>(wearer);

        if (TryComp<ThermalVisibleComponent>(wearer, out var thermal))
            thermal.DrawWhenInvisible = false;
    }

    public bool IsCloaked(EntityUid wearer)
    {
        if (!_hidden.ContainsValue(wearer))
            return false;

        return !(TryComp<NinjaCloakRevealedComponent>(wearer, out var reveal) && reveal.Shown);
    }
}
