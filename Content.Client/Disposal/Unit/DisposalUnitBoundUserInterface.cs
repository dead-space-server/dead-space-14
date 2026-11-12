using Content.Client.Power.EntitySystems;
using Content.Client.UserInterface.Controls;
using Content.Shared.Access.Systems;
using Content.Shared.DeadSpace.Demons.DemonShadow.Components;
using Content.Shared.Disposal.Components;
using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Client.UserInterface;

namespace Content.Client.Disposal.Unit
{
    [UsedImplicitly]
    public sealed class DisposalUnitBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
    {
<<<<<<< ours
        [Dependency] private readonly IPlayerManager _playerManager = default!; // DS14

        [ViewVariables] private DisposalUnitWindow? _disposalUnitWindow;

        public DisposalUnitBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        private void ButtonPressed(DisposalUnitComponent.UiButton button)
        {
            SendPredictedMessage(new DisposalUnitComponent.UiButtonPressedMessage(button));
            // If we get client-side power stuff then we can predict the button presses but for now we won't as it stuffs
            // the pressure lerp up.
        }
||||||| base
        [ViewVariables] private DisposalUnitWindow? _disposalUnitWindow;

        public DisposalUnitBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        private void ButtonPressed(DisposalUnitComponent.UiButton button)
        {
            SendPredictedMessage(new DisposalUnitComponent.UiButtonPressedMessage(button));
            // If we get client-side power stuff then we can predict the button presses but for now we won't as it stuffs
            // the pressure lerp up.
        }
=======
        [ViewVariables]
        private DisposalUnitWindow? _disposalUnitWindow;
>>>>>>> theirs

        protected override void Open()
        {
            base.Open();
            _disposalUnitWindow = this.CreateWindow<DisposalUnitWindow>();
            _disposalUnitWindow.OpenCenteredRight();
            _disposalUnitWindow.SetInfoFromEntity(EntMan, Owner);

            _disposalUnitWindow.OnTogglePower += ButtonPressed;
            _disposalUnitWindow.OnEject += ButtonPressed;
            _disposalUnitWindow.OnEngage += ButtonPressed;

            _disposalUnitWindow.OnChangeRouting += OpenRoutingWindow;

            Update();
        }

        public override void Update()
        {
            base.Update();

            if (_disposalUnitWindow == null)
                return;

            // Update general fields
            if (!EntMan.TryGetComponent<DisposalUnitComponent>(Owner, out var component))
                return;

            var disposalSystem = EntMan.System<DisposalUnitSystem>();
            _disposalUnitWindow.Populate(
                    EntMan.System<PowerReceiverSystem>().IsPowered(Owner),
                    component.Engaged,
                    disposalSystem.GetState((Owner, component)),
                    disposalSystem.EstimatedFullPressure((Owner, component)),
                    component.PressurePerSecond);

            // Update if routing is enabled or accessible
            if (!EntMan.TryGetComponent<DisposalTaggerComponent>(Owner, out var tagger))
            {
                _disposalUnitWindow.PopulateRouting(false, false);
                return;
            }

            var hasAccess = false;
            if (PlayerManager.LocalEntity != null)
            {
                var accessReader = EntMan.System<AccessReaderSystem>();
                hasAccess = accessReader.IsAllowed((EntityUid)PlayerManager.LocalEntity, Owner);
            }

            _disposalUnitWindow.PopulateRouting(tagger.Editable, hasAccess);
        }

        private void ButtonPressed(DisposalUnitUiButton button)
        {
            SendPredictedMessage(new DisposalUnitUiButtonPressedMessage(button));
        }

<<<<<<< ours
            _disposalUnitWindow.UnitState.Text = Loc.GetString($"disposal-unit-state-{state}");
            _disposalUnitWindow.Power.Pressed = EntMan.System<PowerReceiverSystem>().IsPowered(Owner);
            _disposalUnitWindow.Engage.Pressed = entity.Comp.Engaged;
            _disposalUnitWindow.FullPressure = disposalSystem.EstimatedFullPressure(entity.Owner, entity.Comp);

            // DS14-start
            var hideControls = IsLocalDemonShadow();
            _disposalUnitWindow.Eject.Visible = !hideControls;
            _disposalUnitWindow.Eject.Disabled = hideControls;
            _disposalUnitWindow.Engage.Visible = !hideControls;
            _disposalUnitWindow.Engage.Disabled = hideControls;
            _disposalUnitWindow.Power.Visible = !hideControls;
            _disposalUnitWindow.Power.Disabled = hideControls;
            // DS14-end
        }

        // DS14-start
        private bool IsLocalDemonShadow()
        {
            return _playerManager.LocalEntity is { } player &&
                   EntMan.HasComponent<DemonShadowComponent>(player);
||||||| base
            _disposalUnitWindow.UnitState.Text = Loc.GetString($"disposal-unit-state-{state}");
            _disposalUnitWindow.Power.Pressed = EntMan.System<PowerReceiverSystem>().IsPowered(Owner);
            _disposalUnitWindow.Engage.Pressed = entity.Comp.Engaged;
            _disposalUnitWindow.FullPressure = disposalSystem.EstimatedFullPressure(entity.Owner, entity.Comp);
=======
        private void OpenRoutingWindow()
        {
            SendPredictedMessage(new DisposalTaggerOpenUiMessage());
>>>>>>> theirs
        }
        // DS14-end
    }
}
