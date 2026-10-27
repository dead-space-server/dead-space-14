using Content.Client.Eui;
using Content.Shared.DeadSpace.HandPull;
using Content.Shared.Mindshield.Components;
using Robust.Client.Graphics;

namespace Content.Client.DeadSpace.HandPull;

public sealed class HandPullOfferSystem : EntitySystem
{
    private YesNoWindow? _window;
    private int? _requstID;
    private bool _serverClosing;

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
        _requstID = msg.RequestID;
        _window = new YesNoWindow(Loc.GetString("hand-pull-window-title"), Loc.GetString("hand-pull-window-message", ("user", msg.UserName)));
        _window.YesButton.Text = Loc.GetString("hand-pull-window-accept");
        _window.NoButton.Text = Loc.GetString("hand-pull-window-decline");
        _window.YesButton.OnPressed += _ => SendAnswer(true);
        _window.NoButton.OnPressed += _ => SendAnswer(false);
        _window.OnClose += OnWindowClosed;

        IoCManager.Resolve<IClyde>().RequestWindowAttention();
        _window.OpenCentered();
    }

    private void OnOfferClosed(HandPullOfferClosedMessage msg, EntitySessionEventArgs args)
    {
        if (_requstID != msg.RequestID)
            return;
        CloseWindow();
    }

    private void OnWindowClosed()
    {
        if (_serverClosing)
            return;
        SendAnswer(false);
    }

    private void SendAnswer(bool accepted)
    {
        if (_requstID is not { } requestID)
            return;
        RaiseNetworkEvent(new HandPullAnswerMessage(requestID, accepted));
        CloseWindow();
    }

    private void CloseWindow()
    {
        _requstID = null;

        if (_window == null)
            return;
        _serverClosing = true;
        _window.Close();
        _serverClosing = false;
        _window = null;
    }
}
