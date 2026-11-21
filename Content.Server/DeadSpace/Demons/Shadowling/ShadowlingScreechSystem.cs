// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Demons.Shadowling;
using Content.Shared.Stunnable;
using Content.Shared.Humanoid;
using Content.Server.Chat.Systems;
using Content.Shared.Chat;

namespace Content.Server.DeadSpace.Demons.Shadowling;

public sealed partial class ShadowlingScreechSystem : EntitySystem
{
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShadowlingScreechComponent, ShadowlingScreechEvent>(OnScreech);
    }

    private void OnScreech(EntityUid uid, ShadowlingScreechComponent component, ShadowlingScreechEvent args)
    {
        if (args.Handled) return;

        _chat.TrySendInGameICMessage(uid, "кричит!", InGameICChatType.Emote, ChatTransmitRange.Normal);

        foreach (var target in _lookup.GetEntitiesInRange(uid, component.Range))
        {
            if (target == uid) continue;

            if (!HasComp<HumanoidAppearanceComponent>(target)) continue;

            if (HasComp<ShadowlingRecruitComponent>(target) ||
                HasComp<ShadowlingSlaveComponent>(target) ||
                HasComp<ShadowlingRevealComponent>(target) ||
                HasComp<ShadowlingComponent>(target))
                continue;

            _stun.TryUpdateParalyzeDuration(target, TimeSpan.FromSeconds(component.StunDuration));
        }

        args.Handled = true;
    }
}