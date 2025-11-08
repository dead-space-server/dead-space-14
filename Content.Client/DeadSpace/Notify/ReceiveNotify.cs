using Content.Shared.Ghost.SharedGhostPing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Audio;
using Content.Client.DeadSpace.NotifySystem.NotifyHelpers;
using Content.Shared.DeadSpace.CCCCVars;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.GameObjects;


namespace Content.Client.DeadSpace.NotifySystem.RecievNotify;

public sealed partial class ReceiveNotifySys : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    public SoundSpecifier SoundNotify = new SoundPathSpecifier("/Audio/Effects/adminhelp.ogg");

    private DateTime _lastNotifyTime = DateTime.MinValue;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PingMessage>(CheckRecievedNotify);
        NotifyHelper.EnsureInitialized(_cfg, _prototypeManager);
    }

    private void CheckReceiveddNotify(PingMessage messege)
    {
        if (NotifyHelper.GetValueAccess(messege.ID) && _cfg.GetCVar(CCCCVars.SysNotifyPerm))
        {
            if (DateTime.Now - _lastNotifyTime >= TimeSpan.FromSeconds(_cfg.GetCVar(CCCCVars.SysNotifyCoolDown)))
            {
                _audio.PlayGlobal(SoundNotify, Filter.Local(), false);
                _lastNotifyTime = DateTime.Now;
            }
        }
    }
}