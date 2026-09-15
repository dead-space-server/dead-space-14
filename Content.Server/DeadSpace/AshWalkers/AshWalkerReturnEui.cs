using Content.Server.EUI;
using Content.Shared.DeadSpace.AshWalkers;
using Content.Shared.DeadSpace.UI;
using Content.Shared.Eui;

namespace Content.Server.DeadSpace.AshWalkers;

public sealed class AshWalkerReturnEui : BaseEui
{
    private readonly AshWalkerRitualSystem _system;
    public EntityUid Rune { get; }
    public EntityUid Invoker { get; }
    public AshWalkerRitualDoAfterEvent Request { get; }
    public TimeSpan ExpiresAt { get; }

    public AshWalkerReturnEui(AshWalkerRitualSystem system, EntityUid rune, EntityUid invoker,
        AshWalkerRitualDoAfterEvent request, TimeSpan expiresAt)
    {
        _system = system;
        Rune = rune;
        Invoker = invoker;
        Request = request;
        ExpiresAt = expiresAt;
    }

    public override EuiStateBase GetNewState()
    {
        return new YesNoEuiState(Loc.GetString("ash-walker-return-title"), Loc.GetString("ash-walker-return-message"));
    }

    public override void Opened() => StateDirty();

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);
        _system.ConfirmReturn(this, msg is YesNoChoiceMessage { Button: YesNoUiButton.Yes });
        Close();
    }

    public override void Closed()
    {
        _system.ConfirmReturn(this, false);
        base.Closed();
    }
}
