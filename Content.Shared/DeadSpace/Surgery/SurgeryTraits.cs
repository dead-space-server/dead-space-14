// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.HealthExaminable;
using Content.Shared.Popups;
using Content.Shared.Speech;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Surgery;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryOrganDonorComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryPhobiaComponent : Component
{
    [DataField]
    public float ScreamEvery = 4f;

    [DataField]
    public float FlinchChance = 0.9f;

    public float Accumulator;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryMuteComponent : Component
{
    [DataField]
    public LocId Popup = "surgery-voice-missing";
}

public sealed partial class SurgeryTraitsSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryOrganDonorComponent, HealthBeingExaminedEvent>(OnHealthExamined);
        SubscribeLocalEvent<SurgeryMuteComponent, SpeakAttemptEvent>(OnSpeak);
    }

    private void OnSpeak(Entity<SurgeryMuteComponent> ent, ref SpeakAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        _popup.PopupEntity(Loc.GetString(ent.Comp.Popup), ent, ent);
        args.Cancel();
    }

    private void OnHealthExamined(Entity<SurgeryOrganDonorComponent> ent, ref HealthBeingExaminedEvent args)
    {
        args.Message.PushNewline();
        args.Message.AddMarkupOrThrow(Loc.GetString("surgery-organ-donor-examine"));
    }
}
