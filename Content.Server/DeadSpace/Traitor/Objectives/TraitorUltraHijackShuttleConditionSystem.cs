// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Shuttles.Systems;
using Content.Shared.Objectives.Components;

namespace Content.Server.DeadSpace.Traitor.Objectives;

public sealed partial class TraitorUltraHijackShuttleConditionSystem : EntitySystem
{
    [Dependency] private EmergencyShuttleSystem _emergencyShuttle = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TraitorUltraHijackShuttleConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(Entity<TraitorUltraHijackShuttleConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = _emergencyShuttle.IsTraitorUltraHijackCompleted(args.MindId) ? 1f : 0f;
    }
}
