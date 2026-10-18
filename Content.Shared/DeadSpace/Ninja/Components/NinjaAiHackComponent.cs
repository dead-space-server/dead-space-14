using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent]
public sealed partial class NinjaAiHackComponent : Component
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(30);

    [DataField]
    public LocId StartedMessage = "ninja-ai-hack-started";

    [DataField]
    public LocId HalfHackMessage = "ninja-ai-hack-half-announcement";

    [DataField]
    public LocId HackedMessage = "ninja-ai-hack-announcement";

    [DataField]
    public LocId NoAiMessage = "ninja-ai-hack-no-ai";

    [DataField]
    public LocId AnnouncementMessage = "ninja-ai-hack-crew-announcement";

    [DataField]
    public LocId AnnouncementSender = "ninja-ai-hack-crew-announcement-sender";

    [DataField]
    public Color AnnouncementColor = Color.FromHex("#1d8bad");

    [DataField]
    public SoundSpecifier? AnnouncementSound = new SoundPathSpecifier("/Audio/_DeadSpace/Effects/Random/phone-gone-crazy-in-the-cathedral.ogg");

    [DataField]
    public EntProtoId EffectPrototype = "EffectSparks";

    [DataField]
    public LocId? HalfwayPopup;

    public DoAfterId? ActiveDoAfter;

    public byte TriggeredStages;
}
