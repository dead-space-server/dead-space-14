// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Power.EntitySystems;
using Content.Shared.UserInterface;
using Content.Shared.Power;
using Robust.Server.GameObjects;
using Content.Server.DeadSpace.ERTCall;
using Content.Shared.DeadSpace.ERT;
using Content.Shared.DeadSpace.TimeWindow;
using Content.Server.Chat.Systems;
using Robust.Shared.Timing;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Shuttles.Events;
using Content.Shared.Chat;
using Content.Shared.Station.Components;

namespace Content.Server.DeadSpace.ERT;

public sealed class ErtComputerShuttleSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly PowerReceiverSystem _powerReceiverSystem = default!;
    [Dependency] private readonly TimedWindowSystem _timedWindowSystem = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ShuttleSystem _shuttleSystem = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ErtComputerShuttleComponent, ErtComputerShuttleUiButtonPressedMessage>(OnButtonPressed);
        SubscribeLocalEvent<ErtComputerShuttleComponent, AfterActivatableUIOpenEvent>(OnUIOpen);
        SubscribeLocalEvent<ErtComputerShuttleComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<DeleteAfterFtlCompleteComponent, FTLCompletedEvent>(OnFTLCompleted);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ErtComputerShuttleComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (component.IsEvacuation)
            {
                if (!_timedWindowSystem.IsExpired(component.EvacuationWindow))
                {
                    if (_timing.CurTime < component.NextAnnounceTime)
                        continue;

                    var time = component.EvacuationWindow.Remaining - _timing.CurTime;
                    var seconds = Math.Max(0, (int)Math.Ceiling(time.TotalSeconds));

                    _chatSystem.TrySendInGameICMessage(
                            uid,
                            Loc.GetString("ert-computer-time-until-eval", ("time", seconds.ToString())),
                            InGameICChatType.Speak,
                            ChatTransmitRange.Normal,
                            true
                        );

                    component.NextAnnounceTime = _timing.CurTime + TimeSpan.FromSeconds(1);
                }
                else
                {
                    component.IsEvacuation = false;

                    var shuttleUid = Transform(uid).GridUid;

                    if (shuttleUid == null)
                        continue;

                    if (!TryComp(shuttleUid.Value, out ShuttleComponent? shuttleComp))
                        continue;

                    if (HasComp<StationMemberComponent>(shuttleUid.Value))
                        continue;

                    if (!_shuttleSystem.CanFTL(shuttleUid.Value, out _))
                        continue;

                    var xform = Transform(shuttleUid.Value);

                    EnsureComp<DeleteAfterFtlCompleteComponent>(shuttleUid.Value);

                    _shuttleSystem.FTLToCoordinates(
                        shuttleUid.Value,
                        shuttleComp,
                        xform.Coordinates,
                        xform.LocalRotation
                    );
                }
            }
        }
    }

    private void OnFTLCompleted(EntityUid uid, DeleteAfterFtlCompleteComponent component, FTLCompletedEvent args)
    {
        QueueDel(uid);
    }

    private void OnButtonPressed(EntityUid uid, ErtComputerShuttleComponent component, ErtComputerShuttleUiButtonPressedMessage args)
    {
        if (!_powerReceiverSystem.IsPowered(uid))
            return;

        switch (args.Button)
        {
            case ErtComputerShuttleUiButton.Evacuation:
                {
                    _timedWindowSystem.Reset(component.EvacuationWindow);
                    component.IsEvacuation = true;
                    break;
                }
            case ErtComputerShuttleUiButton.CancelEvacuation:
                {
                    component.IsEvacuation = false;
                    break;
                }
            default:
                break;
        }

        UpdateUserInterface((uid, component));
    }

    private void OnPowerChanged(EntityUid uid, ErtComputerShuttleComponent component, ref PowerChangedEvent args)
    {
        UpdateUserInterface((uid, component));
    }


    private void OnUIOpen(EntityUid uid, ErtComputerShuttleComponent component, AfterActivatableUIOpenEvent args)
    {
        UpdateUserInterface((uid, component));
    }

    public void UpdateUserInterface(Entity<ErtComputerShuttleComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return;

        if (!TryComp<UserInterfaceComponent>(entity, out var userInterface))
            return;

        if (!_uiSystem.HasUi(entity, ErtComputerShuttleUiKey.Key, userInterface))
            return;

        if (!_powerReceiverSystem.IsPowered(entity))
        {
            _uiSystem.CloseUis((entity, userInterface));
            return;
        }

        var newState = GetUserInterfaceState((entity, entity.Comp));
        _uiSystem.SetUiState((entity, userInterface), ErtComputerShuttleUiKey.Key, newState);
    }

    private ErtComputerShuttleBoundUserInterfaceState GetUserInterfaceState(Entity<ErtComputerShuttleComponent?> console)
    {
        if (!Resolve(console, ref console.Comp, false))
            return default!;

        return new ErtComputerShuttleBoundUserInterfaceState();
    }


}

