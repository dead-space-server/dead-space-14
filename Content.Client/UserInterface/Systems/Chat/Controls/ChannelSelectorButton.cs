using System.Numerics;
using Content.Client.DeadSpace.Stylesheets;
using Content.Shared.Chat;

namespace Content.Client.UserInterface.Systems.Chat.Controls;

public sealed class ChannelSelectorButton : ChatPopupButton<ChannelSelectorPopup>
{
    public event Action<ChatSelectChannel>? OnChannelSelect;

    public ChatSelectChannel SelectedChannel { get; private set; }
    private Shared.Radio.RadioChannelPrototype? _selectedRadio; // DS14

    private const int SelectorDropdownOffset = 38;

    public ChannelSelectorButton()
    {
        Name = "ChannelSelector";

        Popup.Selected += OnChannelSelected;

        if (Popup.FirstChannel is { } firstSelector)
        {
            Select(firstSelector);
        }
    }

    protected override UIBox2 GetPopupPosition()
    {
        var globalLeft = GlobalPosition.X;
        var globalBot = GlobalPosition.Y + Height;
        return UIBox2.FromDimensions(
            new Vector2(globalLeft, globalBot),
            new Vector2(SizeBox.Width, SelectorDropdownOffset));
    }

    private void OnChannelSelected(ChatSelectChannel channel)
    {
        Select(channel);
    }

    public void Select(ChatSelectChannel channel)
    {
        if (Popup.Visible)
        {
            Popup.Close();
        }

        if (SelectedChannel == channel)
            return;
        SelectedChannel = channel;
        OnChannelSelect?.Invoke(channel);
    }

    public static string ChannelSelectorName(ChatSelectChannel channel)
    {
        return Loc.GetString($"hud-chatbox-select-channel-{channel}");
    }

    public Color ChannelSelectColor(ChatSelectChannel channel)
    {
        return channel switch
        {
            ChatSelectChannel.Radio => Color.LimeGreen,
            ChatSelectChannel.LOOC => Color.MediumTurquoise,
            ChatSelectChannel.OOC => Color.LightSkyBlue,
            ChatSelectChannel.Dead => Color.MediumPurple,
            ChatSelectChannel.Admin => Color.HotPink,
            _ => DeadSpaceStylePalette.TextOnTranscriptMuted // DS14
        };
    }

    public void UpdateChannelSelectButton(ChatSelectChannel channel, Shared.Radio.RadioChannelPrototype? radio)
    {
        _selectedRadio = radio; // DS14
        Text = radio != null ? Loc.GetString(radio.Name) : ChannelSelectorName(channel);
        Modulate = radio?.Color ?? ChannelSelectColor(channel);
    }

    // DS14-start
    protected override void StylePropertiesChanged()
    {
        base.StylePropertiesChanged();
        Modulate = _selectedRadio?.Color ?? ChannelSelectColor(SelectedChannel);
    }
    // DS14-end
}
