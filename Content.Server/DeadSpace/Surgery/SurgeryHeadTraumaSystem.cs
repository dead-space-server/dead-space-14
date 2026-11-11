// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Psychiatry;
using Content.Shared.DeadSpace.Psychiatry;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.Popups;

namespace Content.Server.DeadSpace.Surgery;

public sealed class SurgeryHeadTraumaSystem : EntitySystem
{
    private const string Effect = "HeadTrauma";

    [Dependency] private readonly PsychiatrySystem _psychiatry = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SchizophreniaComponent, SurgeryEffectEvent>(OnEffect);
    }

    private void OnEffect(Entity<SchizophreniaComponent> ent, ref SurgeryEffectEvent args)
    {
        if (args.Handled
            || args.Effect != Effect
            || ent.Comp.Kind != PsychiatryIllnessKind.Schizophrenia
            || ent.Comp.Stage <= SchizophreniaStage.None)
            return;

        args.Handled = true;
        _psychiatry.AdjustStage(ent.Owner, -(int) SchizophreniaStage.Acute, "surgery");
        _psychiatry.HoldOnset(ent.Owner);
        _popup.PopupEntity(Loc.GetString("surgery-brain-shocked"), args.User, args.User, PopupType.Medium);
    }
}
