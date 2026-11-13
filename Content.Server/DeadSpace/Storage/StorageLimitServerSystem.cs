using System.Collections.Generic;
using Content.Shared.DeadSpace.Storage;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.DeadSpace.Storage;

public sealed partial class StorageLimitServerSystem : EntitySystem
{
    [Dependency] private IPlayerManager _playerManager = default!;

    private readonly Dictionary<NetUserId, bool> _playerStorageLimits = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<MultipleInventoryWindowsEnabledEvent>(OnMultiInventoryChanged);
        SubscribeLocalEvent<CheckMultipleInventoryWindowsEvent>(OnCheckMultiWindow);
        SubscribeLocalEvent<GetPlayerStorageLimitEvent>(OnGetPlayerStorageLimit);

        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus == SessionStatus.Disconnected)
            _playerStorageLimits.Remove(e.Session.UserId);
    }

    private void OnMultiInventoryChanged(MultipleInventoryWindowsEnabledEvent msg, EntitySessionEventArgs args)
    {
        _playerStorageLimits[args.SenderSession.UserId] = msg.Enabled;
    }

    private void OnCheckMultiWindow(ref CheckMultipleInventoryWindowsEvent ev)
    {
        if (TryComp(ev.Actor, out ActorComponent? actorComp)
            && _playerStorageLimits.TryGetValue(actorComp.PlayerSession.UserId, out var enabled)
            && enabled)
        {
            ev.Enabled = true;
        }
    }

    private void OnGetPlayerStorageLimit(ref GetPlayerStorageLimitEvent ev)
    {
        if (TryComp(ev.Actor, out ActorComponent? actorComp)
            && _playerStorageLimits.TryGetValue(actorComp.PlayerSession.UserId, out var enabled)
            && enabled)
        {
            ev.Limit = -1;
            ev.HasOverride = true;
        }
    }

    public void OnPlayerDisconnect(NetUserId userId)
    {
        _playerStorageLimits.Remove(userId);
    }
}
