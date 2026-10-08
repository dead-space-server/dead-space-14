// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Ninja;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class NinjaAiHackConditionSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaAiHackConditionComponent, ObjectiveAssignedEvent>(OnObjectiveAssigned);
        SubscribeLocalEvent<NinjaAiHackConditionComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
        SubscribeLocalEvent<NinjaAiHackConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
        SubscribeLocalEvent<NinjaAiHackEvent>(OnAiHacked);
    }

    private void OnObjectiveAssigned(Entity<NinjaAiHackConditionComponent> ent, ref ObjectiveAssignedEvent args)
    {
        ent.Comp.Mind = args.MindId;
        ent.Comp.Hacked = false;

        if (args.Mind.OwnedEntity != null)
            return;

        args.Cancelled = true;
    }

    private void OnAfterAssign(Entity<NinjaAiHackConditionComponent> ent, ref ObjectiveAfterAssignEvent args)
    {
        _metaData.SetEntityName(ent, Loc.GetString("objective-condition-ninja-ai-hack-title"), args.Meta);
        _metaData.SetEntityDescription(ent, Loc.GetString("objective-condition-ninja-ai-hack-description"), args.Meta);
    }

    private void OnAiHacked(ref NinjaAiHackEvent args)
    {
        if (!_mind.TryGetMind(args.Ninja, out var ninjaMindId, out _))
            return;

        var query = EntityQueryEnumerator<NinjaAiHackConditionComponent>();
        while (query.MoveNext(out _, out var condition))
        {
            if (condition.Mind is not { } owner || owner != ninjaMindId)
                continue;

            condition.Hacked = true;
        }
    }

    private void OnGetProgress(Entity<NinjaAiHackConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = ent.Comp.Hacked ? 1f : 0f;
    }
}