using Content.Client.Eui;
using Content.Shared.DeadSpace.HandPull;
using Robust.Client.Graphics;

namespace Content.Client.DeadSpace.HandPull;

public sealed class HandPullOfferSystem : EntitySystem
{
    [Dependency] private readonly IClyde _clyde = default!;

    private YesNoWindow? _window;
    private NetEntity? _offerUser;
    private bool _closing;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<HandPullOfferMessage>(OnOffer);
        SubscribeNetworkEvent<HandPullOfferClosedMessage>(OnOfferClosed);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        CloseWindow();
    }

    private void OnOffer(HandPullOfferMessage msg, EntitySessionEventArgs args)
    {
        CloseWindow();

        _offerUser = msg.User;
        _window = new YesNoWindow(
            Loc.GetString("hand-pull-window-title"),
            Loc.GetString("hand-pull-window-message", ("user", msg.UserName)));

        _window.YesButton.Text = Loc.GetString("hand-pull-window-accept");
        _window.NoButton.Text = Loc.GetString("hand-pull-window-decline");

        _window.YesButton.OnPressed += _ => SendAnswer(true);
        _window.NoButton.OnPressed += _ => SendAnswer(false);
        _window.OnClose += () =>
        {
            if (!_closing)
                SendAnswer(false);
        };

        _clyde.RequestWindowAttention();
        _window.OpenCentered();
    }

    private void OnOfferClosed(HandPullOfferClosedMessage msg, EntitySessionEventArgs args)
    {
        if (_offerUser == msg.User)
            CloseWindow();
    }

    private void SendAnswer(bool accepted)
    {
        if (_offerUser is not { } user)
            return;

        RaiseNetworkEvent(new HandPullAnswerMessage(user, accepted));
        CloseWindow();
    }

    private void CloseWindow()
    {
        _offerUser = null;

        if (_window == null)
            return;

        _closing = true;
        _window.Close();
        _closing = false;
        _window = null;
    }
}
