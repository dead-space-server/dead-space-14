// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Body.Part;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Kitchen.Components;
using Robust.Shared.Containers;

namespace Content.Server.DeadSpace.Body.Limbs;

public sealed partial class LimbGrindSystem : EntitySystem
{
    private const string Solution = "part";

    [Dependency] private SharedSolutionContainerSystem _solutions = default!;

    private readonly HashSet<EntityUid> _pending = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyPartComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BodyPartComponent, EntGotRemovedFromContainerMessage>(OnRemoved);
    }

    private void OnMapInit(Entity<BodyPartComponent> ent, ref MapInitEvent args)
    {
        _pending.Add(ent);
    }

    private void OnRemoved(Entity<BodyPartComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        _pending.Add(ent);
    }

    public override void Update(float frameTime)
    {
        if (_pending.Count == 0)
            return;

        foreach (var uid in _pending)
        {
            if (!TerminatingOrDeleted(uid) && TryComp<BodyPartComponent>(uid, out var part) && part.Body == null)
                Grindable((uid, part));
        }

        _pending.Clear();
    }

    private void Grindable(Entity<BodyPartComponent> ent)
    {
        if (ent.Comp.PartType is BodyPartType.Head or BodyPartType.Torso
            || !TryComp<ExtractableComponent>(ent, out var extractable)
            || extractable.GrindableSolution != null
            || extractable.JuiceSolution is not { } juice)
            return;

        if (!_solutions.EnsureSolution(ent.Owner, Solution, juice.Volume, juice.Clone(), out _, out _))
            return;

        extractable.GrindableSolution = Solution;
    }
}
