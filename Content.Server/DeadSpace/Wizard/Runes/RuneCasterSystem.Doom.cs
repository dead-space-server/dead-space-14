// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Chat;
using Content.Shared.DeadSpace.Wizard.Runes;
using Content.Shared.Mind.Components;
using Robust.Shared.Audio;

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
}

public sealed partial class RuneCasterSystem
{
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
        }
    }

    private void Vanish(EntityUid uid, RuneVanishingComponent vanish)
    {
        if (TerminatingOrDeleted(uid))
            return;

        var coords = Transform(uid).Coordinates;
        if (_inventory.TryGetSlots(uid, out var slots))
        {
            foreach (var slot in slots)
                _inventory.TryUnequip(uid, slot.Name, true, true);
        }

        foreach (var held in _hands.EnumerateHeld(uid).ToList())
            _hands.TryDrop(uid, held, checkActionBlocker: false);

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
