// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.DeadSpace.Reactions;
using Content.Shared.DeadSpace.Sandevistan;
using Content.Shared.Mobs;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Reactions;

public sealed partial class NearbyCriticalShoutSystem : EntitySystem
{
    [Dependency] private readonly SharedSandevistanSystem _sandevistan = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Critical || args.OldMobState == MobState.Critical)
            return;

        var curTime = _timing.CurTime;
        var targetPosition = Transform(args.Target).WorldPosition;
        var query = EntityQueryEnumerator<NearbyCriticalShoutComponent>();
        while (query.MoveNext(out var host, out var reaction))
        {
            if (host == args.Target ||
                reaction.Messages.Count == 0 ||
                reaction.OnlyWhileActive && !_sandevistan.IsActive(host) ||
                curTime < reaction.NextShout)
            {
                continue;
            }

            var distance = (Transform(host).WorldPosition - targetPosition).Length();
            if (distance > reaction.Range)
                continue;

            _chat.TrySendInGameICMessage(
                host,
                Loc.GetString(_random.Pick(reaction.Messages)),
                InGameICChatType.Speak,
                ChatTransmitRange.Normal,
                checkRadioPrefix: false,
                ignoreActionBlocker: true);
            reaction.NextShout = curTime + TimeSpan.FromSeconds(MathF.Max(0f, reaction.Cooldown));
        }
    }
}
