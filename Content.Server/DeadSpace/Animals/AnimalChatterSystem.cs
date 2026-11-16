using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.Mobs.Systems;
using Content.Shared.Random.Helpers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Animals;

public sealed partial class AnimalChatterSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private MobStateSystem _mobs = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnimalChatterComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<AnimalChatterComponent> ent, ref MapInitEvent args)
    {
        Schedule(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AnimalChatterComponent>();
        while (query.MoveNext(out var uid, out var chatter))
        {
            if (chatter.NextChatter > now)
                continue;

            Schedule((uid, chatter));

            if (!_mobs.IsAlive(uid) || HasComp<ActorComponent>(uid))
                continue;

            var message = _random.Pick(_prototypes.Index(chatter.Dataset));
            if (string.IsNullOrWhiteSpace(message))
                continue;

            _chat.TrySendInGameICMessage(uid, message, InGameICChatType.Emote, hideChat: false, hideLog: true);
        }
    }

    private void Schedule(Entity<AnimalChatterComponent> ent)
    {
        ent.Comp.NextChatter = _timing.CurTime + _random.Next(ent.Comp.MinInterval, ent.Comp.MaxInterval);
    }
}
