using Content.Shared.Corvax.JoinQueue;
using Robust.Client.State;
using Robust.Shared.Network;

namespace Content.Client.Corvax.JoinQueue;

public sealed partial class JoinQueueManager
{
    [Dependency] private IClientNetManager _netManager = default!;
    [Dependency] private IStateManager _stateManager = default!;

    public void Initialize()
    {
        _netManager.RegisterNetMessage<MsgQueueUpdate>(OnQueueUpdate);
    }

    private void OnQueueUpdate(MsgQueueUpdate msg)
    {
        if (_stateManager.CurrentState is not QueueState)
        {
            _stateManager.RequestStateChange<QueueState>();
        }

        ((QueueState) _stateManager.CurrentState).OnQueueUpdate(msg);
    }
}