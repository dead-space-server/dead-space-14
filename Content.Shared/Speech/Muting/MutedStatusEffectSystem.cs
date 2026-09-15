using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Popups;
using Content.Shared.Speech;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Shared.Speech.Muting;

/// <summary>
/// Handles the speech restrictions imposed by <see cref="MutedStatusEffectComponent"/>.
/// </summary>
public sealed partial class MutedStatusEffectSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    /// <inheritdoc />
    public override void Initialize()
    {
        SubscribeLocalEvent<MutedStatusEffectComponent, StatusEffectRelayedEvent<SpeakAttemptEvent>>(OnSpeakAttempt);
        SubscribeLocalEvent<MutedStatusEffectComponent, StatusEffectRelayedEvent<EmoteEvent>>(OnEmote);
        // DS14-start: the current baseline still uses ScreamActionEvent instead of EmoteActionEvent.
        SubscribeLocalEvent<MutedStatusEffectComponent, StatusEffectRelayedEvent<ScreamActionEvent>>(OnScreamAction);
        // DS14-end
    }

    private void OnEmote(Entity<MutedStatusEffectComponent> ent, ref StatusEffectRelayedEvent<EmoteEvent> args)
    {
        if (args.Args.Handled)
            return;

        // Still leaves the text so it looks like they are pantomiming a laugh.
        if (args.Args.Emote.Category.HasFlag(EmoteCategory.Vocal))
        {
            args.Args = args.Args with { Handled = true };
        }
    }

    // DS14-start
    private void OnScreamAction(Entity<MutedStatusEffectComponent> ent, ref StatusEffectRelayedEvent<ScreamActionEvent> args)
    {
        if (args.Args.Handled)
            return;

        if (!TryComp<StatusEffectComponent>(ent, out var statusEffect))
            return;

        if (statusEffect.AppliedTo is not { } target)
            return;

        _popup.PopupEntity(Loc.GetString(ent.Comp.ActionPopup), target, target);
        args.Args.Handled = true;
    }
    // DS14-end

    private void OnSpeakAttempt(Entity<MutedStatusEffectComponent> ent, ref StatusEffectRelayedEvent<SpeakAttemptEvent> args)
    {
        if (args.Args.Cancelled)
            return;

        var target = args.Args.Uid;

        _popup.PopupEntity(Loc.GetString(ent.Comp.SpeakPopup), target, target);

        args.Args.Cancel();
    }
}
