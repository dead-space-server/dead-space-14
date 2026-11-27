// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Client.UserInterface.Controllers;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Robust.Shared.Input.Binding;
using Content.Shared.Input;
using Robust.Client.UserInterface.Controls;
using Robust.Client.Player;
using Content.Shared.DeadSpace.Languages;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Client.UserInterface;
using Robust.Client.GameObjects;
using Content.Shared.DeadSpace.Languages.Components;
using System.Linq;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Language;

public sealed partial class LanguageUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    [Dependency] private IPlayerManager _playerMan = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IUserInterfaceManager _userInterfaceManager = default!;
    private Radial.Controls.RadialContainer? _openedMenu;
    private const string DefaultIcon = "/Textures/_DeadSpace/LanguageIcons/default.png";
    private bool _isClosing;
    private MenuButton? LanguageButton => UIManager.GetActiveUIWidgetOrNull<MenuBar.Widgets.GameTopMenuBar>()?.LanguageButton;

    public override void Initialize()
    {
        base.Initialize();
    }

    public void OnStateEntered(GameplayState state)
    {
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.OpenLanguageMenu,
                InputCmdHandler.FromDelegate(_ => { ToggleMenu(); }))
            .Register<LanguageUIController>();

        if (LanguageButton == null)
            return;

        LanguageButton.OnPressed -= ActionButtonPressed;
        LanguageButton.OnPressed += ActionButtonPressed;
    }

    public void OnStateExited(GameplayState state)
    {
        CommandBinds.Unregister<LanguageUIController>();

        if (LanguageButton == null)
            return;

        LanguageButton.OnPressed -= ActionButtonPressed;

        _openedMenu = null;
    }

    private void ActionButtonPressed(BaseButton.ButtonEventArgs args)
    {
        if (LanguageButton == null)
            return;

        ToggleMenu();
    }

    private void Close()
    {
        if (_isClosing)
            return;

        _isClosing = true;

        _openedMenu?.Dispose();
        _openedMenu = null;

        _isClosing = false;
    }

    private void ToggleMenu()
    {
        if (_openedMenu != null)
        {
            Close();
            return;
        }

        if (_playerMan.LocalEntity == null)
            return;

        if (!EntityManager.TryGetComponent<LanguageComponent>(_playerMan.LocalEntity, out var component))
            return;

        var spriteSystem = EntityManager.System<SpriteSystem>();

        _openedMenu = _userInterfaceManager.GetUIController<Radial.RadialUiController>()
            .CreateRadialContainer();

        var speakableLanguages = component.KnownLanguages
            .Except(component.CantSpeakLanguages)
            .ToList();

        if (!speakableLanguages.Any())
            return;

        foreach (var protoId in speakableLanguages)
        {
            if (_proto.TryIndex(protoId, out var prototype))
            {
                if (prototype == null)
                    return;

                var actionName = prototype.Name;
                var texturePath = spriteSystem.Frame0(new SpriteSpecifier.Texture(new ResPath(DefaultIcon)));

                if (prototype.Icon != null)
                    texturePath = spriteSystem.Frame0(prototype.Icon);

                var emoteButton = _openedMenu.AddButton(actionName, texturePath);
                emoteButton.Opacity = 210;
                emoteButton.Tooltip = null;
                emoteButton.Controller.OnPressed += (_) =>
                {
                    EntityManager.RaisePredictiveEvent(new SelectLanguageEvent(_playerMan.LocalEntity.Value.Id, protoId));

                    _openedMenu.Dispose();
                };
            }
        }

        _openedMenu.OnAttached += (_) =>
        {
            if (LanguageButton != null)
                LanguageButton.Pressed = true;
        };
        _openedMenu.OnDetached += (_) =>
        {
            if (LanguageButton != null)
                LanguageButton.Pressed = false;
        };
        _openedMenu.OnClose += (_) =>
        {
            Close();
        };
        if (_playerMan.LocalEntity != null)
        {
            _openedMenu.OpenAttached((EntityUid)_playerMan.LocalEntity);
        }

    }

}
