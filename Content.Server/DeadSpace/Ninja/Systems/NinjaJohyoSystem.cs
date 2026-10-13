// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Chat.Systems;
using Content.Shared.DeadSpace.Ninja.Systems;
using Content.Shared.DeadSpace.Ninja.Components;
using Robust.Shared.Random;
using Content.Shared.Chat;
using System.Linq;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class ChainGunSystem : SharedChainGunSystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    protected override void SayPhrase(EntityUid user, NinjaJohyoComponent comp)
    {
        if (!comp.ShotPhrases.Any())
            return;

        var phrase = _random.Pick(comp.ShotPhrases);
        _chat.TrySendInGameICMessage(
            user,
            Loc.GetString(phrase),
            InGameICChatType.Speak,
            hideChat: false);
    }
}