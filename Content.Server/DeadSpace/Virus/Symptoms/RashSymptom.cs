// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.DeadSpace.Virus.Symptoms;
using Content.Shared.DeadSpace.Virus.Components;
using Content.Shared.DeadSpace.TimeWindow;
using Content.Shared.DeadSpace.Virus.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Virus.Symptoms;

public sealed partial class RashSymptom : VirusSymptomBase
{
    [Dependency] private EntityManager _entityManager = default!;
    public override VirusSymptom Type => VirusSymptom.Rash;
    protected override ProtoId<VirusSymptomPrototype> PrototypeId => "RashSymptom";
    private const string RashEmote = "чешется";

    public RashSymptom(TimedWindow effectTimedWindow) : base(effectTimedWindow)
    { }

    public override void OnAdded(EntityUid host, VirusComponent virus)
    {
        base.OnAdded(host, virus);
    }

    public override void OnRemoved(EntityUid host, VirusComponent virus)
    {
        base.OnRemoved(host, virus);
    }

    public override void OnUpdate(EntityUid host, VirusComponent virus)
    {
        base.OnUpdate(host, virus);
    }

    public override void DoEffect(EntityUid host, VirusComponent virus)
    {
        var chatSystem = _entityManager.System<ChatSystem>();

        chatSystem.TrySendInGameICMessage(host,
                            RashEmote,
                            InGameICChatType.Emote,
                            ChatTransmitRange.Normal);
    }

    public override void ApplyDataEffect(VirusData data, bool add)
    {
        base.ApplyDataEffect(data, add);
    }

    public override IVirusSymptom Clone()
    {
        return new RashSymptom(EffectTimedWindow.Clone());
    }
}
