using Content.Server.Chat.Systems;
using Content.Shared.DeadSpace.MartialArts;
using Content.Shared.DeadSpace.SmokingCarp;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.MartialArts;

public sealed partial class MartialArtsSystem : SharedMartialArtsSystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MartialArtsComponent, SmokingCarpSaying>(OnSmokingCarpSaying);
    }
    private void OnSmokingCarpSaying(Entity<MartialArtsComponent> ent, ref SmokingCarpSaying args)
    {
        if (!_proto.TryIndex(args.Saying, out var messagePack))
            return;

        var message = Loc.GetString(_random.Pick(messagePack.Values));

        _chat.TrySendInGameICMessage(ent, Loc.GetString(args.Saying), InGameICChatType.Speak, false);
    }
}
