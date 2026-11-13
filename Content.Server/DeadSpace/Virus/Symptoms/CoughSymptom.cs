// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Chat.Systems;
using Content.Shared.DeadSpace.Virus.Symptoms;
using Content.Shared.DeadSpace.Virus.Components;
using Content.Server.DeadSpace.Virus.Systems;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.DeadSpace.TimeWindow;
using Robust.Shared.Prototypes;
using Content.Shared.DeadSpace.Virus.Prototypes;
namespace Content.Server.DeadSpace.Virus.Symptoms;

public sealed partial class CoughSymptom : VirusSymptomBase
{
    [Dependency] private EntityManager _entityManager = default!;
    public override VirusSymptom Type => VirusSymptom.Cough;
    protected override ProtoId<VirusSymptomPrototype> PrototypeId => "CoughSymptom";
    private static readonly ProtoId<EmotePrototype> CoughEmote = "Cough";

    public CoughSymptom(TimedWindow effectTimedWindow) : base(effectTimedWindow)
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
        var virusSystem = _entityManager.System<VirusSystem>();

        // Почему-то проигрывается вместо со звуком, хотя раньше такого не было
        chatSystem.TryEmoteWithChat(host,
                            CoughEmote,
                            ChatTransmitRange.HideChat,
                            ignoreActionBlocker: true);

        virusSystem.InfectAround(host);
    }

    public override void ApplyDataEffect(VirusData data, bool add)
    {
        base.ApplyDataEffect(data, add);
    }

    public override IVirusSymptom Clone()
    {
        return new CoughSymptom(EffectTimedWindow.Clone());
    }
}
