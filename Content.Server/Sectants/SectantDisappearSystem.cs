using Content.Server.Chat.Systems;
using Content.Shared.Chat;                  // InGameICChatType
using Content.Shared.Mobs;
using Content.Shared.Sectants;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.Sectants;

public sealed class SectantDisappearSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SectantDisappearComponent, MobStateChangedEvent>(OnMobState);
    }

    private void OnMobState(EntityUid uid, SectantDisappearComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        var xform = Transform(uid);
        var coords = _xform.GetMapCoordinates(uid, xform);
        var msg = Loc.GetString(comp.Message);

        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } player) continue;
            if (!TryComp<TransformComponent>(player, out var pxform)) continue;
            var pcoords = _xform.GetMapCoordinates(player, pxform);
            if ((pcoords.Position - coords.Position).Length() > comp.Radius) continue;
            _chat.TrySendInGameICMessage(player, msg, InGameICChatType.Speak, hideChat: false, hideLog: false);
        }

        if (comp.Sound != null)
            _audio.PlayPvs(comp.Sound, uid);

        if (comp.EffectPrototype is { } effect)
            Spawn(effect, coords);

        RaiseLocalEvent(new SectantDisappearedEvent(uid, comp.Message, comp.Radius));

        if (comp.DeleteEntity)
            EntityManager.QueueDeleteEntity(uid);
    }
}
