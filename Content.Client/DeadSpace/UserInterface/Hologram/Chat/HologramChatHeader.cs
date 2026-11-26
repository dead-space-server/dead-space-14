// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using System.Text;
using Content.Client.UserInterface.Systems.Chat;
using Content.Client.UserInterface.Systems.Chat.Controls;
using Content.Shared.Chat;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Chat;

/// <summary>
/// Header strip of the projected HUD chat: the "СВЯЗЬ" title, the channels the player can speak on right now and a
/// close control that drops the message being typed. Shown only while <see cref="HologramChatPanel.Framed"/>.
/// </summary>
public sealed class HologramChatHeader : PanelContainer
{
    public const string StyleClassHeader = "DeadSpaceChatHeader";
    public const string StyleClassTitle = "DeadSpaceChatHeaderTitle";
    public const string StyleClassChannels = "DeadSpaceChatHeaderChannels";
    public const string StyleClassClose = "DeadSpaceChatClose";
    public const string StyleClassCloseIcon = "DeadSpaceChatCloseIcon";

    private const string Separator = " · ";
    private static readonly ResPath CloseIcon = new("/Textures/Interface/Nano/cross.svg.png");

    private readonly ChatUIController _controller;
    private readonly Label _channels;
    private bool _subscribed;

    /// <summary>The close control was pressed.</summary>
    public event Action? CloseRequested;

    public HologramChatHeader()
    {
        _controller = UserInterfaceManager.GetUIController<ChatUIController>();
        AddStyleClass(StyleClassHeader);
        HorizontalExpand = true;

        var title = new Label
        {
            Text = Loc.GetString("hologram-chat-header-title"),
            StyleClasses = { StyleClassTitle },
            VerticalAlignment = VAlignment.Center,
        };

        _channels = new Label
        {
            StyleClasses = { StyleClassChannels },
            ClipText = true,
            HorizontalExpand = true,
            // Pass: the full list shows as a tooltip, and clicks still reach the chat's resize edge.
            MouseFilter = MouseFilterMode.Pass,
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(10, 1, 6, 0),
        };

        var close = new ContainerButton
        {
            StyleClasses = { StyleClassClose },
            SetSize = new Vector2(22, 22),
            VerticalAlignment = VAlignment.Center,
            ToolTip = Loc.GetString("hologram-chat-close-tooltip"),
        };
        close.AddChild(new TextureRect
        {
            TexturePath = CloseIcon.ToString(),
            TextureScale = new Vector2(0.5f, 0.5f),
            Stretch = TextureRect.StretchMode.KeepCentered,
            StyleClasses = { StyleClassCloseIcon },
        });
        close.OnPressed += _ => CloseRequested?.Invoke();

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };
        row.AddChild(title);
        row.AddChild(_channels);
        row.AddChild(close);
        AddChild(row);

        UpdateChannels(_controller.SelectableChannels);
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();

        if (!_subscribed)
        {
            _controller.SelectableChannelsChanged += UpdateChannels;
            _subscribed = true;
        }

        UpdateChannels(_controller.SelectableChannels);
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();

        if (!_subscribed)
            return;

        _controller.SelectableChannelsChanged -= UpdateChannels;
        _subscribed = false;
    }

    private void UpdateChannels(ChatSelectChannel selectable)
    {
        var text = new StringBuilder();
        foreach (var channel in ChannelSelectorPopup.ChannelSelectorOrder)
        {
            if ((selectable & channel) == 0)
                continue;

            if (text.Length > 0)
                text.Append(Separator);

            text.Append(ChannelSelectorButton.ChannelSelectorName(channel));
        }

        var channels = text.ToString().ToUpperInvariant();
        _channels.Text = channels;
        _channels.ToolTip = channels;
    }
}
