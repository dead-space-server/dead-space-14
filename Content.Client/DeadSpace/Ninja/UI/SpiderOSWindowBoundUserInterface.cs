// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.DeadSpace.Ninja.Systems;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.DeadSpace.Ninja.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Client.DeadSpace.Ninja.UI;

public sealed class SpiderOSWindowBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IEntitySystemManager _systems = default!;

    [ViewVariables]
    private SpiderOSWindow? _window;

    private SpiderOSSystem? _bootSystem;
    private bool _bootActive;

    private bool _lockActive;

    public SpiderOSWindowBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        IoCManager.InjectDependencies(this);

        _window = new SpiderOSWindow();

        _window.OnModuleSelected += (tier, category) =>
        {
            SendMessage(new SpiderOSSelectModuleMessage(tier, category));
        };

        _window.OnAppearanceChanged += (colorway, helmet, style) =>
        {
            SendMessage(new SpiderOSSetAppearanceMessage(colorway, helmet, style));
        };

        _window.OnSuitPowerChanged += activated =>
        {
            if (!_bootActive)
            {
                StartBoot(activated);
            }
        };

        _window.OnShuttleControl += () =>
        {
            SendMessage(new SpiderOSShuttleControlMessage());
        };

        _window.OnClose += Close;
        _window.OpenCentered();
    }

    private void StartBoot(bool activated)
    {
        _bootSystem ??= _systems.GetEntitySystem<SpiderOSSystem>();

        if (_window == null || !EntMan.TryGetComponent(Owner, out SpiderOSComponent? comp))
        {
            return;
        }

        var bootId = activated ? comp.ActivationBootScript : comp.DeactivationBootScript;

        if (!_proto.TryIndex(bootId, out SpiderOSBootPrototype? boot))
        {
            return;
        }

        _bootActive = true;
        _window.ShowBootView();

        _bootSystem.StartBoot(Owner, boot, activated, new SpiderOSSystem.SpiderOSBootCallbacks
        {
            OnLog = (log, args) =>
            {
                _window.AppendBootLog(args is { Length: > 0 } ? Loc.GetString(log, args) : Loc.GetString(log), SpiderOSWindow.BootLogColor);
            },
            OnProgress = progress =>
            {
                _window.SetBootProgress(progress);
            },
            OnCheckFailed = failReason =>
            {
                if (!string.IsNullOrEmpty(failReason.Id))
                    _window.AppendBootLog(Loc.GetString(failReason), SpiderOSWindow.BootErrorColor);
                _window.AppendBootLog(Loc.GetString("spider-os-boot-rollback"), SpiderOSWindow.BootLogColor);
            },
            OnSecureRequest = (secure, check) =>
            {
                SendMessage(new SpiderOSSecureRequestMessage(secure, check));
                _lockActive = secure;
            },
            OnLockRollback = () =>
            {
                SendMessage(new SpiderOSSecureRequestMessage(false, SpiderOSBootCheck.VisorSecure));
                _lockActive = false;
            },
            OnFinished = () =>
            {
                SendMessage(new SpiderOSSetSuitPowerMessage(activated));
            },
            OnConfirmed = FinishBoot,
            OnRolledBack = FinishBoot,
        });
    }

    private void FinishBoot()
    {
        _bootActive = false;
        _lockActive = false;
        _window?.ShowMainView();
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);

        if (message is SpiderOSSecureConfirmedMessage confirm)
        {
            _bootSystem?.ConfirmSecure(Owner, confirm.Success, confirm.FailReason);
        }
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null || state is not SpiderOSBoundUserInterfaceState cState)
            return;

        if (!_proto.TryIndex(cState.Skills, out SpiderOSPrototype? proto))
            return;

        _window.UpdateState(
            cState.LockedTiers,
            cState.SelectedModules,
            cState.ActivatedTiers,
            proto.AllSkills,
            cState.PendingColorway,
            cState.PendingHelmet,
            cState.PendingStyle,
            cState.SuitActivated);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _bootSystem?.CancelBoot(Owner);
            if (_lockActive)
                SendMessage(new SpiderOSSecureRequestMessage(false, SpiderOSBootCheck.VisorSecure));
        }

        base.Dispose(disposing);

        if (!disposing)
            return;

        _window?.Close();
        _window = null;
    }
}