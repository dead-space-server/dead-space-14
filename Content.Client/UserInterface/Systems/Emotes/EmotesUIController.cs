using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Input;
using Content.Shared.Speech;
using Content.Shared.Whitelist;
using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input.Binding;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Emotes;

[UsedImplicitly]
public sealed class EmotesUIController : UIController, IOnStateChanged<GameplayState>
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private MenuButton? EmotesButton => UIManager.GetActiveUIWidgetOrNull<MenuBar.Widgets.GameTopMenuBar>()?.EmotesButton;
    private SimpleRadialMenu? _menu;

    // DS14-start
    private readonly Dictionary<string, string> _localizedNames = new();
    private readonly Dictionary<string, SpriteSpecifier> _spriteCache = new();
    private const string DefaultIcon = "/Textures/Interface/AdminActions/play.png";
    private bool _menuOpening;
    // DS14-end

    private static readonly Dictionary<EmoteCategory, (string Tooltip, SpriteSpecifier Sprite)> EmoteGroupingInfo =
        new()
        {
            [EmoteCategory.General] = ("emote-menu-category-general",
                new SpriteSpecifier.Rsi(new ResPath("/Textures/Clothing/Head/Soft/mimesoft.rsi"), "icon")),
            [EmoteCategory.Hands] = ("emote-menu-category-hands",
                new SpriteSpecifier.Rsi(new ResPath("/Textures/Clothing/Hands/Gloves/latex.rsi"), "icon")),
            [EmoteCategory.Vocal] = ("emote-menu-category-vocal",
                new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Emotes/vocal.png"))),
            // DS14-start
            [EmoteCategory.Animations] = ("emote-menu-category-animations",
                new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/AdminActions/play.png"))),
            // DS14-end
        };

    public void OnStateEntered(GameplayState state)
    {
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.OpenEmotesMenu,
                InputCmdHandler.FromDelegate(_ => ToggleEmotesMenu(false)))
            .Register<EmotesUIController>();

        // DS14-start
        LoadButton();
        // DS14-end
    }

    public void OnStateExited(GameplayState state)
    {
        CommandBinds.Unregister<EmotesUIController>();

        // DS14-start
        UnloadButton();
        CloseMenu();
        // DS14-end
    }

    private void ToggleEmotesMenu(bool centered)
    {
        // DS14-start
        if (_menu != null || _menuOpening)
            return;

        _menuOpening = true;
        try
        {
            // DS14-end

            var prototypes = _prototypeManager.EnumeratePrototypes<EmotePrototype>();
            var models = ConvertToButtons(prototypes);

            _menu = new SimpleRadialMenu();
            _menu.SetButtons(models);

            _menu.Open();

            _menu.OnClose += OnWindowClosed;
            _menu.OnOpen += OnWindowOpen;

            if (EmotesButton != null)
                EmotesButton.SetClickPressed(true);

            if (centered)
            {
                _menu.OpenCentered();
            }
            else
            {
                _menu.OpenOverMouseScreenPosition();
            }

            // DS14-start
        }
        finally
        {
            _menuOpening = false;
        }
            // DS14-end
    }

    public void UnloadButton()
    {
        if (EmotesButton == null)
            return;

        EmotesButton.OnPressed -= ActionButtonPressed;
    }

    public void LoadButton()
    {
        if (EmotesButton == null)
            return;

        EmotesButton.OnPressed -= ActionButtonPressed;
        EmotesButton.OnPressed += ActionButtonPressed;
    }

    private void ActionButtonPressed(BaseButton.ButtonEventArgs args)
    {
        ToggleEmotesMenu(true);
    }

    private void OnWindowClosed()
    {
        if (EmotesButton != null)
            EmotesButton.Pressed = false;

        CloseMenu();
    }

    private void OnWindowOpen()
    {
        if (EmotesButton != null)
            EmotesButton.Pressed = true;
    }

    private void CloseMenu()
    {
        if (_menu == null)
            return;

        _menu.OnClose -= OnWindowClosed;
        _menu.OnOpen -= OnWindowOpen;
        _menu.Dispose();
        _menu = null;
    }

    private IEnumerable<RadialMenuOptionBase> ConvertToButtons(IEnumerable<EmotePrototype> emotePrototypes)
    {
        var whitelistSystem = EntitySystemManager.GetEntitySystem<EntityWhitelistSystem>();
        var player = _playerManager.LocalSession?.AttachedEntity;

        Dictionary<EmoteCategory, List<RadialMenuOptionBase>> emotesByCategory = new();

        foreach (var emote in emotePrototypes)
        {
            if (emote.Category == EmoteCategory.Invalid)
                continue;

            // only valid emotes that have ways to be triggered by chat and player have access / no restriction on
            if (emote.Category == EmoteCategory.Invalid
                || emote.ChatTriggers.Count == 0
                || !(player.HasValue && whitelistSystem.IsWhitelistPassOrNull(emote.Whitelist, player.Value))
                || whitelistSystem.IsBlacklistPass(emote.Blacklist, player.Value))
                continue;

            if (!emote.Available
                && EntityManager.TryGetComponent<SpeechComponent>(player.Value, out var speech)
                && !speech.AllowedEmotes.Contains(emote.ID))
                continue;

            if (!emotesByCategory.TryGetValue(emote.Category, out var list))
            {
                list = new List<RadialMenuOptionBase>();
                emotesByCategory.Add(emote.Category, list);
            }

            // DS14-start
            if (!_localizedNames.TryGetValue(emote.Name, out var localizedName))
            {
                localizedName = Loc.GetString(emote.Name);
                _localizedNames[emote.Name] = localizedName;
            }

            if (!_spriteCache.TryGetValue(emote.ID, out var sprite))
            {
                sprite = emote.Icon ?? new SpriteSpecifier.Texture(new ResPath(DefaultIcon));
                _spriteCache[emote.ID] = sprite;
            }
            // DS14-end

            var actionOption = new RadialMenuActionOption<EmotePrototype>(HandleRadialButtonClick, emote)
            {
                // DS14-start
                IconSpecifier = RadialMenuIconSpecifier.With(sprite),
                ToolTip = localizedName
                // DS14-end
            };
            list.Add(actionOption);
        }

        var models = new RadialMenuOptionBase[emotesByCategory.Count];
        var i = 0;
        foreach (var (key, list) in emotesByCategory)
        {
            if (!EmoteGroupingInfo.TryGetValue(key, out var tuple))
                continue;

            models[i] = new RadialMenuNestedLayerOption(list)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(tuple.Sprite),
                ToolTip = Loc.GetString(tuple.Tooltip)
            };
            i++;
        }

        return models;
    }

    private void HandleRadialButtonClick(EmotePrototype prototype)
    {
        _entityManager.RaisePredictiveEvent(new PlayEmoteMessage(prototype.ID));
    }
}
