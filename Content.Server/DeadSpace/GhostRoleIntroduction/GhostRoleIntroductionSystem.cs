// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.CustomizableHumanoidSpawner;
using Content.Shared.DeadSpace.GhostRoleIntroduction;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server.DeadSpace.GhostRoleIntroduction;

public sealed class GhostRoleIntroductionSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GhostRoleIntroductionComponent, MindAddedMessage>(OnMindAdded);
    }

    private void OnMindAdded(Entity<GhostRoleIntroductionComponent> ent, ref MindAddedMessage args)
    {
        // Customizable ghost spawners receive the mind before the final fighter is created.
        if (HasComp<CustomizableHumanoidSpawnerComponent>(ent))
            return;

        if (!_player.TryGetSessionById(args.Mind.Comp.UserId, out var session))
            return;

        var text = Loc.GetString(ent.Comp.Text);
        RaiseNetworkEvent(new GhostRoleIntroductionEvent(
            text,
            ent.Comp.Duration,
            ent.Comp.FadeFromBlackDuration,
            ent.Comp.TextDelay,
            ent.Comp.CharactersPerSecond), session);

        if (ent.Comp.Sound != null)
            _audio.PlayGlobal(ent.Comp.Sound, Filter.Empty().AddPlayer(session), false);
    }
}