using Content.Shared.DeadSpace.AshWalkers;
using Robust.Client;

namespace Content.Client.DeadSpace.AshWalkers;

public sealed class AshWalkerLobbySystem : EntitySystem
{
    [Dependency] private readonly IBaseClient _client = default!;

    public bool Waiting { get; private set; }
    public event Action? StateChanged;

    public override void Initialize()
    {
        SubscribeNetworkEvent<AshWalkerLobbyStateEvent>(OnState);
        _client.RunLevelChanged += OnRunLevelChanged;
    }

    public override void Shutdown()
    {
        _client.RunLevelChanged -= OnRunLevelChanged;
        base.Shutdown();
    }

    private void OnState(AshWalkerLobbyStateEvent args)
    {
        Waiting = args.Waiting;
        StateChanged?.Invoke();
    }

    private void OnRunLevelChanged(object? sender, RunLevelChangedEventArgs args)
    {
        if (args.NewLevel != ClientRunLevel.Initialize)
            return;

        Waiting = false;
        StateChanged?.Invoke();
    }

    public void RequestState() => RaiseNetworkEvent(new AshWalkerLobbyStateRequestEvent());
    public void Cancel() => RaiseNetworkEvent(new AshWalkerLobbyCancelEvent());
}
