using Content.Shared.Ghost.SharedGhostPing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Audio;
using Content.Client.Options.UI.Tabs;

namespace Content.Client.DeadSpace.NotifySystem.RecievNotify;

public sealed partial class RecievNotifySys : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    public SoundSpecifier SoundNotify = new SoundPathSpecifier("/Audio/Effects/adminhelp.ogg");
    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PingMessege>(CheckRecievedNotify);
    }

    private void CheckRecievedNotify(PingMessege messege)
    {
        if (PingTab.GetValueAccess(messege.ID))
        {
            _audio.PlayGlobal(SoundNotify, Filter.Local(), false);
        }
    }
}