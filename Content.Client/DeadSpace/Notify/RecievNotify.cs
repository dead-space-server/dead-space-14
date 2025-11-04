using Content.Shared.Ghost.SharedGhostPing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Audio;
using Content.Client.DeadSpace.NotifySystem.NotifyHelpers;
using Content.Shared.DeadSpace.CCCCVars;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;


namespace Content.Client.DeadSpace.NotifySystem.RecievNotify;

public sealed partial class RecievNotifySys : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    public SoundSpecifier SoundNotify = new SoundPathSpecifier("/Audio/Effects/adminhelp.ogg");

    private DateTime IGameTiming = DateTime.Now;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PingMessege>(CheckRecievedNotify);
        NotifyHelper.EnsureInitialized(_cfg, _prototypeManager);
    }

    private void CheckRecievedNotify(PingMessege messege)
    {
        if (NotifyHelper.GetValueAccess(messege.ID) && _cfg.GetCVar(CCCCVars.SysNotifyPerm))
        {
            if (IGameTiming.AddSeconds(_cfg.GetCVar(CCCCVars.SysNotifyCoolDown)) < DateTime.Now)
            {
                _audio.PlayGlobal(SoundNotify, Filter.Local(), false);
                IGameTiming = DateTime.Now;
            }
        }
    }
}