// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.NewLife;
using Robust.Client;
using Robust.Shared.Player;

namespace Content.Client.DeadSpace.NewLife;

public sealed class NewLifeSystem : EntitySystem
{
    [Dependency] private readonly IBaseClient _client = default!;
    public NewLifeState State { get; private set; }
    public event Action? StateChanged;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<NewLifeStateEvent>(OnState);
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);
        _client.RunLevelChanged += OnRunLevelChanged;
    }

    public override void Shutdown()
    {
        _client.RunLevelChanged -= OnRunLevelChanged;
        base.Shutdown();
    }

    private void OnState(NewLifeStateEvent args)
    {
        State = args.State;
        StateChanged?.Invoke();
    }

    private void OnPlayerAttached(LocalPlayerAttachedEvent args)
    {
        RaiseNetworkEvent(new NewLifeStateRequestEvent());
    }

    private void OnRunLevelChanged(object? sender, RunLevelChangedEventArgs args)
    {
        if (args.NewLevel != ClientRunLevel.Initialize)
            return;

        State = default;
        StateChanged?.Invoke();
    }

    public void OpenWindow() => RaiseNetworkEvent(new NewLifeOpenEvent());
}
