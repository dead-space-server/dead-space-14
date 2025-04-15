using Content.Client.Audio;
using Content.Client.DeadSpace.Armutant.Component;
using Content.Shared.DeadSpace.Armutant.Objectives.System;
using Robust.Client.Player;
using Robust.Shared.GameStates;

namespace Content.Client.DeadSpace.Armutant;

public sealed class ObjectiveCreateMapSystem : SharedObjectiveCreateMapSystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly ContentAudioSystem _audio = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayAmbientMusicEvent>(OnPlayAmbientMusic);
        SubscribeLocalEvent<ObjectiveCreateMapComponent, ComponentHandleState>(OnExpeditionHandleState);
    }

    private void OnExpeditionHandleState(Entity<ObjectiveCreateMapComponent> uid, ref ComponentHandleState args)
    {
        _audio.DisableAmbientMusic();
    }

    private void OnPlayAmbientMusic(ref PlayAmbientMusicEvent ev)
    {
        if (ev.Cancelled)
            return;

        var player = _playerManager.LocalEntity;

        if (!TryComp(player, out TransformComponent? xform) ||
            !TryComp<ObjectiveCreateMapComponent>(xform.MapUid, out _))
        {
            return;
        }

        ev.Cancelled = true;
    }
}
