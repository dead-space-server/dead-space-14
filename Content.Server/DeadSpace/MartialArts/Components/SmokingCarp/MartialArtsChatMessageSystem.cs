// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Server.DeadSpace.MartialArts.SmokingCarp.Components;
using Content.Shared.DeadSpace.MartialArts.SmokingCarp;
using Content.Server.DeadSpace.MartialArts.CQC.Components;
using Content.Shared.DeadSpace.MartialArts.CQC;

namespace Content.Server.DeadSpace.MartialArts;

public sealed partial class MartialArtsChatMessageSystem : SharedMartialArtsSystem
{
    [Dependency] private ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SmokingCarpComponent, SmokingCarpSaying>(OnSmokingCarpSaying);
        SubscribeLocalEvent<CQCComponent, CQCSaying>(OnCQCSaying);
    }

    private void OnSmokingCarpSaying(Entity<SmokingCarpComponent> ent, ref SmokingCarpSaying args)
    {
        _chat.TrySendInGameICMessage(ent, Loc.GetString(args.Saying), InGameICChatType.Speak, false);
    }

    private void OnCQCSaying(Entity<CQCComponent> ent, ref CQCSaying args)
    {
        _chat.TrySendInGameICMessage(ent, Loc.GetString(args.Saying), InGameICChatType.Speak, false);
    }
}