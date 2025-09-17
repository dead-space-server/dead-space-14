using System.Collections.Generic;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Shared.ADT.EmotePanel;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input.Binding;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.ADT.EmotePanel;

public sealed class EmotePanelSystem : UIController, IOnStateChanged<GameplayState>
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private MenuButton? _emotesButton;
    private SimpleRadialMenu? _menu;
    private bool _menuOpening;

    private const string DefaultIcon = "/Textures/Interface/AdminActions/play.png";

    private static readonly IReadOnlyDictionary<EmoteCategory, (string Tooltip, SpriteSpecifier Sprite)>
        EmoteGroupingInfo =
            new Dictionary<EmoteCategory, (string Tooltip, SpriteSpecifier Sprite)>
            {
                [EmoteCategory.General] = ("emote-menu-category-general",
                    new SpriteSpecifier.Rsi(new ResPath("/Textures/Clothing/Head/Soft/mimesoft.rsi"), "icon")),
                [EmoteCategory.Hands] = ("emote-menu-category-hands",
                    new SpriteSpecifier.Rsi(new ResPath("/Textures/Clothing/Hands/Gloves/latex.rsi"), "icon")),
                [EmoteCategory.Vocal] = ("emote-menu-category-vocal",
                    new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Emotes/vocal.png"))),
                [EmoteCategory.Animations] = ("emote-menu-category-animations",
                    new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/AdminActions/play.png"))),
            };

    private readonly Dictionary<string, string> _localizedNames = new();
    private readonly Dictionary<string, SpriteSpecifier> _spriteCache = new();

    public override void Initialize()
    {
        SubscribeNetworkEvent<RequestEmoteMenuEvent>(OnRequestEmoteMenu);
    }

    public void OnStateEntered(GameplayState state)
    {
        _emotesButton ??= UIManager
            .GetActiveUIWidgetOrNull<Content.Client.UserInterface.Systems.MenuBar.Widgets.GameTopMenuBar>()
            ?.EmotesButton;

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.OpenEmotesMenu,
                InputCmdHandler.FromDelegate(_ => ToggleEmotesMenu()))
            .Register<EmotePanelSystem>();

        LoadButton();
    }

    public void OnStateExited(GameplayState state)
    {
        CommandBinds.Unregister<EmotePanelSystem>();
        UnloadButton();
        CloseMenu();
        _emotesButton = null;
    }

    private void ToggleEmotesMenu()
    {
        if (_menu != null || _menuOpening)
            return;

        var player = _playerManager.LocalSession?.AttachedEntity;
        if (!player.HasValue)
            return;

        _menuOpening = true;
        try
        {
            var netEntity = _entityManager.GetNetEntity(player.Value);
            _entityManager.RaisePredictiveEvent(new RequestEmoteMenuEvent(netEntity));
        }
        finally
        {
            _menuOpening = false;
        }
    }

    private void OnRequestEmoteMenu(RequestEmoteMenuEvent args, EntitySessionEventArgs session)
    {
        var player = _playerManager.LocalSession?.AttachedEntity;
        if (!player.HasValue || player.Value != _entityManager.GetEntity(args.Target))
            return;

        var models = ConvertToButtons(args.Prototypes);

        _menu = new SimpleRadialMenu();
        _menu.SetButtons(models);
        _menu.OnClose += OnWindowClosed;
        _menu.OpenCentered();

        _emotesButton?.SetClickPressed(true);
    }

    public void UnloadButton()
    {
        if (_emotesButton is null)
            return;

        _emotesButton.OnPressed -= ActionButtonPressed;
    }

    public void LoadButton()
    {
        if (_emotesButton is null)
            return;

        _emotesButton.OnPressed -= ActionButtonPressed;
        _emotesButton.OnPressed += ActionButtonPressed;
    }

    private void ActionButtonPressed(BaseButton.ButtonEventArgs args)
    {
        ToggleEmotesMenu();
    }

    private void OnWindowClosed()
    {
        if (_emotesButton != null)
            _emotesButton.Pressed = false;

        CloseMenu();
    }

    private void CloseMenu()
    {
        if (_menu == null)
            return;

        _menu.OnClose -= OnWindowClosed;
        _menu.Dispose();
        _menu = null;
    }

    private IEnumerable<RadialMenuOption> ConvertToButtons(List<string> emoteIds)
    {
        var emotesByCategory = new Dictionary<EmoteCategory, List<RadialMenuOption>>(EmoteGroupingInfo.Count);
        foreach (var category in EmoteGroupingInfo.Keys)
            emotesByCategory[category] = new List<RadialMenuOption>();

        for (int i = 0; i < emoteIds.Count; i++)
        {
            var emoteId = emoteIds[i];
            if (!_prototypeManager.TryIndex<EmotePrototype>(emoteId, out var emote))
                continue;

            if (!emotesByCategory.TryGetValue(emote.Category, out var list))
                continue;

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

            var option = new RadialMenuActionOption<EmotePrototype>(HandleRadialButtonClick, emote)
            {
                Sprite = sprite,
                ToolTip = localizedName
            };
            list.Add(option);
        }

        foreach (var kv in emotesByCategory)
        {
            if (kv.Value.Count == 0 || !EmoteGroupingInfo.TryGetValue(kv.Key, out var info))
                continue;

            yield return new RadialMenuNestedLayerOption(kv.Value)
            {
                Sprite = info.Sprite,
                ToolTip = Loc.GetString(info.Tooltip)
            };
        }
    }

    private void HandleRadialButtonClick(EmotePrototype prototype)
    {
        var player = _playerManager.LocalSession?.AttachedEntity;
        if (!player.HasValue)
            return;

        var netEntity = _entityManager.GetNetEntity(player.Value);
        _entityManager.RaisePredictiveEvent(new PlayEmoteEvent(prototype.ID, netEntity));
    }
}
