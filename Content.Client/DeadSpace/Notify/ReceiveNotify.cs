//Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Shared.DeadSpace.Ghost.SharedGhostPing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Audio;
using Content.Client.DeadSpace.NotifySystem.NotifyHelpers;
using Content.Shared.DeadSpace.CCCCVars;
using Robust.Shared.Configuration;
using Robust.Shared.Log;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.NotifySystem.RecievNotify;

public sealed partial class ReceiveNotifySystem : EntitySystem
{
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly INotifyHelper _helper = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    //public SoundSpecifier SoundNotify = new SoundPathSpecifier("/Audio/Effects/adminhelp.ogg");

    //private readonly NotifyHelper _helper = NotifyHelperProvider.Helper;
    private ISawmill _sawmill = default!;

    private TimeSpan _lastNotifyTime;


    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logManager.GetSawmill("ReceiveNotifySystem");
        _lastNotifyTime = _timing.RealTime;
        SubscribeNetworkEvent<PingMessage>(CheckReceivedNotify);
        _helper.EnsureInitialized();
    }

    private void CheckReceivedNotify(PingMessage messege)
    {
        if (_cfg.GetCVar(CCCCVars.SysNotifyPerm) && _helper.GetValueAccess(messege.ID))
        {
            if (_timing.RealTime - _lastNotifyTime >= TimeSpan.FromSeconds(_cfg.GetCVar(CCCCVars.SysNotifyCoolDown)))
            {
                _audio.PlayGlobal(new SoundPathSpecifier(_cfg.GetCVar(CCCCVars.SysNotifySoundPath)), Filter.Local(), false);
                _lastNotifyTime = _timing.RealTime;
            }
        }
    }
}