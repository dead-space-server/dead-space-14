// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Chat;
using Content.Shared.DeadSpace.Wizard.Runes;
using Content.Shared.IdentityManagement;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.Wizard.Runes;

[RegisterComponent]
public sealed partial class RuneLastWordsComponent : Component
{
    public readonly List<string> Lines = new();
}

[RegisterComponent]
public sealed partial class RuneEchoComponent : Component
{
    public readonly Queue<(TimeSpan At, string Line)> Lines = new();
}

[RegisterComponent]
public sealed partial class RuneVanishingComponent : Component
{
    public TimeSpan End;

    public SoundSpecifier? Sound;

    public bool KeepItems;
}

[RegisterComponent]
public sealed partial class RuneDanceComponent : Component
{
    public TimeSpan End;

    public float Speed;

    public EntityUid? Target;

    public Vector2 Direction;

    public TimeSpan NextTurn;

    public TimeSpan NextPoint;
}

public sealed partial class RuneCasterSystem
{
    private const float DanceFollowGap = 1.2f;
    private const int LastWordsKept = 5;
    private bool _echoing;

    private void InitializeDoom()
    {
        SubscribeLocalEvent<EntitySpokeEvent>(OnEntitySpoke);
    }

    private void UpdateDoom()
    {
        var now = Timing.CurTime;

        var flashes = EntityQueryEnumerator<RuneScreenFlashComponent>();
        while (flashes.MoveNext(out var uid, out var flash))
        {
            if (now >= flash.End)
                RemCompDeferred<RuneScreenFlashComponent>(uid);
        }

        var echoes = EntityQueryEnumerator<RuneEchoComponent>();
        while (echoes.MoveNext(out var uid, out var echo))
        {
            if (echo.Lines.Count == 0)
            {
                RemCompDeferred<RuneEchoComponent>(uid);
                continue;
            }

            if (now < echo.Lines.Peek().At)
                continue;

            var (_, line) = echo.Lines.Dequeue();
            if (!_mobState.IsDead(uid))
                continue;

            _echoing = true;
            try
            {
                _chat.TrySendInGameICMessage(uid, line, InGameICChatType.Speak, false, ignoreActionBlocker: true);
            }
            finally
            {
                _echoing = false;
            }
        }

        var gone = new List<(EntityUid, RuneVanishingComponent)>();
        var vanishing = EntityQueryEnumerator<RuneVanishingComponent>();
        while (vanishing.MoveNext(out var uid, out var vanish))
        {
            if (now >= vanish.End)
                gone.Add((uid, vanish));
        }

        foreach (var (uid, vanish) in gone)
            Vanish(uid, vanish);

        UpdateDance(now);
    }

    private void ApplyDance(RuneCastContext ctx, RuneDanceEffect effect)
    {
        var now = Timing.CurTime;
        foreach (var target in Targets(ctx, effect))
        {
            if (!_mobState.IsDead(target))
                continue;

            var dance = EnsureComp<RuneDanceComponent>(target);
            dance.End = now + TimeSpan.FromSeconds(effect.Duration * ctx.DurationScale);
            dance.Speed = effect.Speed;
            dance.Target = _random.Prob(effect.FollowChance) ? PickWitness(target, effect.FollowRange) : null;
            dance.NextTurn = now;
            dance.NextPoint = now + TimeSpan.FromSeconds(_random.NextFloat(0.5f, 1.5f));
        }
    }

    private EntityUid? PickWitness(EntityUid corpse, float range)
    {
        var from = _transform.GetMapCoordinates(corpse);
        EntityUid? best = null;
        var bestDistance = float.MaxValue;
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(from, range))
        {
            if (mob.Owner == corpse || !HasComp<ActorComponent>(mob) || _mobState.IsDead(mob))
                continue;

            var distance = (_transform.GetWorldPosition(mob) - from.Position).LengthSquared();
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = mob.Owner;
        }

        return best;
    }

    private void UpdateDance(TimeSpan now)
    {
        var dances = EntityQueryEnumerator<RuneDanceComponent, TransformComponent>();
        while (dances.MoveNext(out var uid, out var dance, out var xform))
        {
            if (now >= dance.End || !_mobState.IsDead(uid))
            {
                _physics.SetLinearVelocity(uid, Vector2.Zero);
                RemCompDeferred<RuneDanceComponent>(uid);
                continue;
            }

            if (_container.IsEntityInContainer(uid) || TryComp<PullableComponent>(uid, out var pull) && pull.Puller != null)
                continue;

            if (dance.Target is { } gone && (TerminatingOrDeleted(gone) || Transform(gone).MapID != xform.MapID))
                dance.Target = null;

            if (dance.Target is { } followed)
            {
                var offset = _transform.GetWorldPosition(followed) - _transform.GetWorldPosition(xform);
                dance.Direction = offset.Length() > DanceFollowGap ? Vector2.Normalize(offset) : Vector2.Zero;
                if (now >= dance.NextPoint)
                {
                    dance.NextPoint = now + TimeSpan.FromSeconds(_random.NextFloat(2f, 3.5f));
                    PointAt(uid, followed);
                }
            }
            else if (now >= dance.NextTurn)
            {
                dance.NextTurn = now + TimeSpan.FromSeconds(_random.NextFloat(0.6f, 1.4f));
                dance.Direction = _random.NextAngle().ToVec();
            }

            _physics.SetLinearVelocity(uid, dance.Direction * dance.Speed);
        }
    }

    private void PointAt(EntityUid pointer, EntityUid pointed)
    {
        var emote = Loc.GetString("rune-dance-point", ("target", Identity.Entity(pointed, EntityManager)));
        _chat.TrySendInGameICMessage(pointer, emote, InGameICChatType.Emote, false, ignoreActionBlocker: true);
        _popup.PopupEntity(Loc.GetString("rune-dance-point-target"), pointed, pointed, PopupType.MediumCaution);
    }

    private void OnEntitySpoke(EntitySpokeEvent args)
    {
        if (_echoing || !HasComp<MindContainerComponent>(args.Source) || _mobState.IsDead(args.Source))
            return;

        var words = EnsureComp<RuneLastWordsComponent>(args.Source);
        words.Lines.Add(args.OriginalMessage);
        if (words.Lines.Count > LastWordsKept)
            words.Lines.RemoveAt(0);
    }

    private void ApplyLastWords(RuneCastContext ctx, RuneLastWordsEffect effect)
    {
        foreach (var target in Targets(ctx, effect))
        {
            if (!_mobState.IsDead(target) || !TryComp<RuneLastWordsComponent>(target, out var words) || words.Lines.Count == 0)
                continue;

            var echo = EnsureComp<RuneEchoComponent>(target);
            echo.Lines.Clear();
            var at = Timing.CurTime;
            foreach (var line in words.Lines.TakeLast(effect.Lines))
            {
                at += TimeSpan.FromSeconds(_random.NextFloat(effect.MinDelay, effect.MaxDelay));
                echo.Lines.Enqueue((at, line));
            }
        }
    }

    private void ApplyScreenFlash(RuneCastContext ctx, RuneScreenFlashEffect effect)
    {
        foreach (var target in Targets(ctx, effect))
        {
            if (target == ctx.Caster)
                continue;

            var flash = EnsureComp<RuneScreenFlashComponent>(target);
            flash.Sprite = effect.Sprite;
            flash.Start = Timing.CurTime;
            flash.End = Timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(effect.MinTime, effect.MaxTime));
            Dirty(target, flash);
        }
    }

    private void Petrify(EntityUid target, Color color)
    {
        EnsureComp<RunePetrifiedComponent>(target);
        _godmode.EnableGodmode(target);
        _flammable.Extinguish(target);
        var tint = EnsureComp<RuneTintComponent>(target);
        tint.Color = color;
        tint.Strength = 1f;
        Dirty(target, tint);
    }

    private void ApplyVanish(RuneCastContext ctx, RuneVanishEffect effect)
    {
        foreach (var target in Targets(ctx, effect))
        {
            if (HasComp<RuneVanishingComponent>(target))
                continue;

            if (effect.Portal is { } portal)
                Spawn(portal, Transform(target).Coordinates);

            if (effect.Emote is { } emote)
                _chat.TryEmoteWithChat(target, emote.Id, ignoreActionBlocker: true, forceEmote: true);

            _stun.TryUpdateParalyzeDuration(target, TimeSpan.FromSeconds(effect.Delay + 1f));
            var vanish = EnsureComp<RuneVanishingComponent>(target);
            vanish.End = Timing.CurTime + TimeSpan.FromSeconds(effect.Delay);
            vanish.Sound = effect.Sound;
            vanish.KeepItems = effect.KeepItems;
        }
    }

    private void Vanish(EntityUid uid, RuneVanishingComponent vanish)
    {
        if (TerminatingOrDeleted(uid))
            return;

        var coords = Transform(uid).Coordinates;
        if (!vanish.KeepItems && _inventory.TryGetSlots(uid, out var slots))
        {
            foreach (var slot in slots)
                _inventory.TryUnequip(uid, slot.Name, true, true);
        }

        if (!vanish.KeepItems)
        {
            foreach (var held in _hands.EnumerateHeld(uid).ToList())
                _hands.TryDrop(uid, held, checkActionBlocker: false);
        }

        if (vanish.Sound != null)
            _audio.PlayPvs(vanish.Sound, coords);

        QueueDel(uid);
    }

    private void ApplyRot(RuneCastContext ctx, RuneRotEffect effect)
    {
        foreach (var target in Targets(ctx, effect))
            EnsureComp<RottingComponent>(target);
    }
}
