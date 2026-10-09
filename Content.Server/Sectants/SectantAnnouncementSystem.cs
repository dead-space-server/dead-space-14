using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Content.Shared.Sectants;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server.Sectants;

public sealed class SectantAnnouncementSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public void Announce(
        string message,
        SoundSpecifier? sound = null,
        Color? color = null,
        float flashDuration = 2.5f,
        float shakeIntensity = 6f)
    {
        var col = color ?? Color.Red;

        _chat.DispatchServerAnnouncement(message, col);

        if (sound != null)
            _audio.PlayGlobal(sound, Filter.Broadcast(), recordReplay: true);

        var ev = new SectantAnnouncementEvent
        {
            Message = message,
            Color = col,
            FlashDuration = flashDuration,
            ShakeIntensity = shakeIntensity,
        };

        foreach (var session in _players.Sessions)
            RaiseNetworkEvent(ev, session.Channel);
    }
}
