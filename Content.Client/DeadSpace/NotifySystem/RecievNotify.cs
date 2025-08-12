using Content.Shared.Ghost.SharedGhostPing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Audio;
using Content.Client.Options.UI.Tabs;

using Robust.Shared.Log;

namespace Content.Client.DeadSpace.NotifySystem.RecievNotify;

public sealed partial class RecievNotifySys : EntitySystem
{
    [Dependency] private readonly ILogManager _logManager = default!;

    private ISawmill _sawmill = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    public SoundSpecifier SoundNotify = new SoundPathSpecifier("/Audio/Effects/adminhelp.ogg");
    public override void Initialize()
    {
        _sawmill = _logManager.GetSawmill("recievNotify");
        base.Initialize();
        SubscribeNetworkEvent<PingMessege>(CheckRecievedNotify);
    }

    private void CheckRecievedNotify(PingMessege messege)
    {
        _sawmill.Debug(messege.ID);
        _sawmill.Debug(PingTab.GetValueAccess(messege.ID).ToString());
        if (PingTab.GetValueAccess(messege.ID))
        {
            _sawmill.Debug("ГООООООООООООООООООООООООЛ");
            _audio.PlayGlobal(SoundNotify, Filter.Local(), false);
        }
    }
}