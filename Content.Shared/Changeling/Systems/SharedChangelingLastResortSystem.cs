using Content.Shared.Mind;
using Content.Shared.Actions;
using Content.Shared.Changeling.Components;
using Content.Shared.Gibbing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared.Changeling.Systems;

public abstract partial class SharedChangelingLastResortSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly GibbingSystem _gibbing = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly DestructionResistanceSystem _destructionResistance = default!;
    [Dependency] protected readonly SharedAudioSystem Audio = default!;

    // DS14-start: pre-v288 RobustToolbox has no event-subscription source generator.
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChangelingSlugComponent, MapInitEvent>(OnTakeOverMapInit);
        SubscribeLocalEvent<ChangelingSlugComponent, ComponentShutdown>(OnTakeOverShutdown);
        SubscribeLocalEvent<ChangelingLastResortAbilityComponent, ChangelingLastResortActionEvent>(OnLastResortAction);
    }
    // DS14-end

    private void OnTakeOverMapInit(Entity<ChangelingSlugComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action);
    }

    private void OnTakeOverShutdown(Entity<ChangelingSlugComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.ActionEntity != null)
            _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);
    }

    private void OnLastResortAction(Entity<ChangelingLastResortAbilityComponent> ent,
        ref ChangelingLastResortActionEvent args)
    {
        if (args.Handled || !_mind.TryGetMind(args.Performer, out var mindId, out var mind))
            return;

        args.Handled = true;

        Audio.PlayPredicted(ent.Comp.Sound, args.Performer, args.Performer);

        if (!_net.IsServer)
            return; // Transfer Mind is unpredictable.

        var slug = PredictedSpawnAtPosition(ent.Comp.SlugPrototype, Transform(args.Performer).Coordinates);
        _mind.MakeSentient(slug);
        _mind.TransferTo(mindId, slug, mind: mind);

        _destructionResistance.SetEnabled(args.Performer, false);

        _gibbing.Gib(args.Performer);
    }
}
