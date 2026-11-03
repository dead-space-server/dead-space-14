using Content.Shared.Eye;
using Content.Shared.SubFloor;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Server.SubFloor;

public sealed class SubFloorHideSystem : SharedSubFloorHideSystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;

    private readonly Dictionary<ICommonSession, VisibilityFlags> _showLayers = new(); //DS-14

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ShowSubfloorRequestEvent>(OnShowSubfloor);
        SubscribeLocalEvent<GetVisMaskEvent>(OnGetVisibility);

        _player.PlayerStatusChanged += OnPlayerStatus;
    }

    private void OnPlayerStatus(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus == SessionStatus.Connected)
            return;

        _showLayers.Remove(e.Session); //DS-14

        if (e.Session.AttachedEntity != null)
            _eye.RefreshVisibilityMask(e.Session.AttachedEntity.Value);
    }

    private void OnGetVisibility(ref GetVisMaskEvent ev)
    {
        if (!TryComp(ev.Entity, out ActorComponent? actor))
            return;

        if (_showLayers.TryGetValue(actor.PlayerSession, out var layers)) //DS-14
            ev.VisibilityMask |= (int) layers; //DS-14
    }

    private void OnShowSubfloor(ShowSubfloorRequestEvent ev, EntitySessionEventArgs args)
    {
        // TODO: Commands are a bit of an eh? for client-only but checking shared perms
        var ent = args.SenderSession.AttachedEntity;

        if (!TryComp(ent, out EyeComponent? eyeComp))
            return;

        //DS-14 start
        var mask = (VisibilityFlags) ev.Mask & SubfloorVisibility.All;
        if (mask == VisibilityFlags.None)
            _showLayers.Remove(args.SenderSession);
        else
            _showLayers[args.SenderSession] = mask;
        //DS-14 end

        _eye.RefreshVisibilityMask((ent.Value, eyeComp));

        RaiseNetworkEvent(new ShowSubfloorRequestEvent()
        {
            Mask = (int) mask, //DS-14
        }, args.SenderSession);
    }
}
