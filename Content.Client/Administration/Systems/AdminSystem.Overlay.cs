using Content.Client.Administration.Managers;
using Content.Shared.CCVar;
using Content.Shared.Ghost;
using Content.Shared.Roles;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client.Administration.Systems
{
    public sealed partial class AdminSystem
    {
        [Dependency] private readonly IOverlayManager _overlayManager = default!;
        [Dependency] private readonly IResourceCache _resourceCache = default!;
        [Dependency] private readonly IClientAdminManager _adminManager = default!;
        [Dependency] private readonly IEyeManager _eyeManager = default!;
        [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
        [Dependency] private readonly IUserInterfaceManager _userInterfaceManager = default!;
        [Dependency] private readonly IConfigurationManager _configurationManager = default!;
        [Dependency] private readonly SharedRoleSystem _roles = default!;
        [Dependency] private readonly IPrototypeManager _proto = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!; // DS14

        private AdminNameOverlay _adminNameOverlay = default!;

        public event Action? OverlayEnabled;
        public event Action? OverlayDisabled;

        private void InitializeOverlay()
        {
            _adminNameOverlay = new AdminNameOverlay(
                this,
                EntityManager,
                _eyeManager,
                _resourceCache,
                _entityLookup,
                _userInterfaceManager,
                _configurationManager,
                _roles,
                _proto);
            _adminManager.AdminStatusUpdated += OnAdminStatusUpdated;
            SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnObserverAttached); // DS14
        }

        private void ShutdownOverlay()
        {
            _adminManager.AdminStatusUpdated -= OnAdminStatusUpdated;
        }

        private void OnAdminStatusUpdated()
        {
            AdminOverlayOff();
            TryEnableObserverOverlay(); // DS14
        }

        // DS14-start
        private void OnObserverAttached(LocalPlayerAttachedEvent args)
        {
            TryEnableObserverOverlay();
        }

        private void TryEnableObserverOverlay()
        {
            if (!_configurationManager.GetCVar(CCVars.AdminOverlayAutoEnableOnObserver) ||
                !_adminManager.IsActive() ||
                _playerManager.LocalEntity is not { } player ||
                !HasComp<GhostComponent>(player))
            {
                return;
            }

            AdminOverlayOn();
        }
        // DS14-end

        public void AdminOverlayOn()
        {
            if (_overlayManager.HasOverlay<AdminNameOverlay>())
                return;
            _overlayManager.AddOverlay(_adminNameOverlay);
            OverlayEnabled?.Invoke();
        }

        public void AdminOverlayOff()
        {
            _overlayManager.RemoveOverlay<AdminNameOverlay>();
            OverlayDisabled?.Invoke();
        }
    }
}
