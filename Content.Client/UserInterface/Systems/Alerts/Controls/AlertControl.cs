using System.Numerics;
using Content.Client.Actions.UI;
using Content.Client.Cooldown;
using Content.Shared.Alert;
using Content.Shared.DeadSpace.ERT.Components;
using Content.Shared.Hands.EntitySystems; // DS14
using Content.Shared.Ninja.Components; // DS14
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Alerts.Controls
{
    public sealed class AlertControl : BaseButton
    {
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!; // DS14

        private readonly SpriteSystem _sprite;

        public AlertPrototype Alert { get; }

        /// <summary>
        /// Current cooldown displayed in this slot. Set to null to show no cooldown.
        /// </summary>
        public (TimeSpan Start, TimeSpan End)? Cooldown
        {
            get => _cooldown;
            set
            {
                _cooldown = value;
                if (SuppliedTooltip is ActionAlertTooltip actionAlertTooltip)
                {
                    actionAlertTooltip.Cooldown = value;
                }
            }
        }

        private (TimeSpan Start, TimeSpan End)? _cooldown;

        private short? _severity;

        private readonly SpriteView _icon;
        private readonly CooldownGraphic _cooldownGraphic;

        private EntityUid _spriteViewEntity;

        /// <summary>
        /// Creates an alert control reflecting the indicated alert + state
        /// </summary>
        /// <param name="alert">alert to display</param>
        /// <param name="severity">severity of alert, null if alert doesn't have severity levels</param>
        public AlertControl(AlertPrototype alert, short? severity)
        {
            // Alerts will handle this.
            MuteSounds = true;

            IoCManager.InjectDependencies(this);
            _sprite = _entityManager.System<SpriteSystem>();
            TooltipSupplier = SupplyTooltip;
            Alert = alert;

            HorizontalAlignment = HAlignment.Left;
            _severity = severity;
            _icon = new SpriteView
            {
                Scale = new Vector2(2, 2),
                MaxSize = new Vector2(64, 64),
                Stretch = SpriteView.StretchMode.None,
                HorizontalAlignment = HAlignment.Left
            };

            SetupIcon();

            Children.Add(_icon);
            _cooldownGraphic = new CooldownGraphic
            {
                MaxSize = new Vector2(64, 64)
            };
            Children.Add(_cooldownGraphic);
        }

        private Control SupplyTooltip(Control? sender)
        {
            // DS14-ERT-Tracking-Start
            if (Alert.ID == "ErtTracking")
            {
                var targetName = Loc.GetString("generic-unknown-title");
                var targetJobName = Loc.GetString("generic-unknown-title");

                if (_playerManager.LocalEntity is { } player &&
                    _entityManager.TryGetComponent<ErtTrackingComponent>(player, out var tracking))
                {
                    targetName = tracking.TargetName ?? targetName;
                    targetJobName = tracking.TargetJobName ?? targetJobName;
                }

                var trackingMsg = FormattedMessage.FromUnformatted(Loc.GetString("ert-tracking-alert-name"));
                var trackingDesc = FormattedMessage.FromUnformatted(Loc.GetString(
                    "ert-tracking-alert-desc",
                    ("target", targetName),
                    ("job", targetJobName)));

                return new ActionAlertTooltip(trackingMsg, trackingDesc) { Cooldown = Cooldown };
            }
            // DS14-ERT-Tracking-End

            // DS14-start
            if (Alert.ID == "DashCooldown")
            {
                var altClick = false;
                if (_playerManager.LocalEntity is { } player)
                {
                    var hands = _entityManager.System<SharedHandsSystem>();
                    foreach (var held in hands.EnumerateHeld(player))
                    {
                        if (_entityManager.TryGetComponent<DashAbilityComponent>(held, out var dash))
                        {
                            altClick = dash.AltClickDash;
                            break;
                        }
                    }
                }

                var dashMsg = FormattedMessage.FromMarkupOrThrow(Loc.GetString(Alert.Name));
                var dashDesc = FormattedMessage.FromMarkupOrThrow(Loc.GetString(
                    altClick ? "dash-alert-desc-alt" : "dash-alert-desc"));
                return new ActionAlertTooltip(dashMsg, dashDesc) { Cooldown = Cooldown };
            }
            // DS14-end

            var msg = FormattedMessage.FromMarkupOrThrow(Loc.GetString(Alert.Name));
            var desc = FormattedMessage.FromMarkupOrThrow(Loc.GetString(Alert.Description));
            return new ActionAlertTooltip(msg, desc) { Cooldown = Cooldown };
        }

        /// <summary>
        /// Change the alert severity, changing the displayed icon
        /// </summary>
        public void SetSeverity(short? severity)
        {
            if (_severity == severity)
                return;
            _severity = severity;

            if (!_entityManager.TryGetComponent<SpriteComponent>(_spriteViewEntity, out var sprite))
                return;
            var icon = Alert.GetIcon(_severity);
            if (_sprite.LayerMapTryGet((_spriteViewEntity, sprite), AlertVisualLayers.Base, out var layer, false))
                _sprite.LayerSetSprite((_spriteViewEntity, sprite), layer, icon);
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);
            UserInterfaceManager.GetUIController<AlertsUIController>().UpdateAlertSpriteEntity(_spriteViewEntity, Alert);

            if (!Cooldown.HasValue)
            {
                _cooldownGraphic.Visible = false;
                _cooldownGraphic.Progress = 0;
                return;
            }

            _cooldownGraphic.FromTime(Cooldown.Value.Start, Cooldown.Value.End);
        }

        private void SetupIcon()
        {
            if (!_entityManager.Deleted(_spriteViewEntity))
                _entityManager.QueueDeleteEntity(_spriteViewEntity);

            _spriteViewEntity = _entityManager.Spawn(Alert.AlertViewEntity);
            if (_entityManager.TryGetComponent<SpriteComponent>(_spriteViewEntity, out var sprite))
            {
                var icon = Alert.GetIcon(_severity);
                if (_sprite.LayerMapTryGet((_spriteViewEntity, sprite), AlertVisualLayers.Base, out var layer, false))
                    _sprite.LayerSetSprite((_spriteViewEntity, sprite), layer, icon);
            }

            _icon.SetEntity(_spriteViewEntity);
        }

        protected override void EnteredTree()
        {
            base.EnteredTree();
            SetupIcon();
        }

        protected override void ExitedTree()
        {
            base.ExitedTree();

            if (!_entityManager.Deleted(_spriteViewEntity))
                _entityManager.QueueDeleteEntity(_spriteViewEntity);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!_entityManager.Deleted(_spriteViewEntity))
                _entityManager.QueueDeleteEntity(_spriteViewEntity);
        }
    }

    public enum AlertVisualLayers : byte
    {
        Base
    }
}
