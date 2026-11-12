<<<<<<< ours
﻿using Content.Server.Actions;
using Content.Server.Humanoid;
||||||| base
﻿using Content.Server.Actions;
using Content.Shared.Body;
=======
using Content.Shared.Actions;
using Content.Shared.Body;
>>>>>>> theirs
using Content.Shared.Cloning.Events;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Mobs;
using Content.Shared.Toggleable;
using JetBrains.Annotations;
using Robust.Shared.Utility;

namespace Content.Shared.Wagging;

/// <summary>
/// Adds an action to toggle the wagging animation for tails markings that support it.
/// </summary>
public sealed partial class WaggingSystem : EntitySystem
{
<<<<<<< ours
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoidAppearance = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WaggingComponent, MapInitEvent>(OnWaggingMapInit);
        SubscribeLocalEvent<WaggingComponent, ComponentShutdown>(OnWaggingShutdown);
        SubscribeLocalEvent<WaggingComponent, ToggleActionEvent>(OnWaggingToggle);
        SubscribeLocalEvent<WaggingComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<WaggingComponent, CloningEvent>(OnCloning);
    }
||||||| base
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly SharedVisualBodySystem _visualBody = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WaggingComponent, MapInitEvent>(OnWaggingMapInit);
        SubscribeLocalEvent<WaggingComponent, ComponentShutdown>(OnWaggingShutdown);
        SubscribeLocalEvent<WaggingComponent, ToggleActionEvent>(OnWaggingToggle);
        SubscribeLocalEvent<WaggingComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<WaggingComponent, CloningEvent>(OnCloning);
    }
=======
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;
>>>>>>> theirs

    /// <summary>
    /// Copies the component and its values to the clone.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnCloning(Entity<WaggingComponent> ent, ref CloningEvent args)
    {
        if (!args.Settings.EventComponents.Contains(Factory.GetRegistration(ent.Comp.GetType()).Name))
            return;

        // Make sure to set the datafields before adding the component so that the correct action gets spawned on map init.
        var cloneComp = Factory.GetComponent<WaggingComponent>();
        cloneComp.Action = ent.Comp.Action;
        cloneComp.Layer = ent.Comp.Layer;
        cloneComp.Organ = ent.Comp.Organ;
        cloneComp.Suffix = ent.Comp.Suffix;
        AddComp(args.CloneUid, cloneComp, true);
    }

<<<<<<< ours
    private void OnWaggingMapInit(EntityUid uid, WaggingComponent component, MapInitEvent args)
||||||| base
    private void OnWaggingMapInit(Entity<WaggingComponent> ent, ref MapInitEvent args)
=======
    /// <summary>
    /// Adds the wagging action during initialization.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnWaggingMapInit(Entity<WaggingComponent> ent, ref MapInitEvent args)
>>>>>>> theirs
    {
<<<<<<< ours
        _actions.AddAction(uid, ref component.ActionEntity, component.Action, uid);
||||||| base
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action, ent);
=======
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action, ent);
        DirtyField(ent.AsNullable(), nameof(WaggingComponent.ActionEntity));
>>>>>>> theirs
    }

<<<<<<< ours
    private void OnWaggingShutdown(EntityUid uid, WaggingComponent component, ComponentShutdown args)
||||||| base
    private void OnWaggingShutdown(Entity<WaggingComponent> ent, ref ComponentShutdown args)
=======
    /// <summary>
    /// Removes the wagging action during shutdown.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnWaggingShutdown(Entity<WaggingComponent> ent, ref ComponentShutdown args)
>>>>>>> theirs
    {
        _actions.RemoveAction(uid, component.ActionEntity);
    }

<<<<<<< ours
    private void OnWaggingToggle(EntityUid uid, WaggingComponent component, ref ToggleActionEvent args)
||||||| base
    private void OnWaggingToggle(Entity<WaggingComponent> ent, ref ToggleActionEvent args)
=======
    /// <summary>
    /// Attempts to toggle the wagging action.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnWaggingToggle(Entity<WaggingComponent> ent, ref ToggleActionEvent args)
>>>>>>> theirs
    {
        if (args.Handled)
            return;

<<<<<<< ours
        TryToggleWagging(uid, wagging: component);
||||||| base
        TryToggleWagging(ent.AsNullable());
=======
        if (!TryToggleWagging(ent.AsNullable()))
            return;

        args.Handled = true;
>>>>>>> theirs
    }

<<<<<<< ours
    private void OnMobStateChanged(EntityUid uid, WaggingComponent component, MobStateChangedEvent args)
||||||| base
    private void OnMobStateChanged(Entity<WaggingComponent> ent, ref MobStateChangedEvent args)
=======
    /// <summary>
    /// Tries to disable wagging when the mob state changes.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnMobStateChanged(Entity<WaggingComponent> ent, ref MobStateChangedEvent args)
>>>>>>> theirs
    {
<<<<<<< ours
        if (component.Wagging)
            TryToggleWagging(uid, wagging: component);
||||||| base
        if (ent.Comp.Wagging)
            TryToggleWagging(ent.AsNullable());
=======
        TryToggleWagging(ent.AsNullable(), false);
>>>>>>> theirs
    }

<<<<<<< ours
    public bool TryToggleWagging(EntityUid uid, WaggingComponent? wagging = null, HumanoidAppearanceComponent? humanoid = null)
||||||| base
    private bool TryToggleWagging(Entity<WaggingComponent?> ent)
=======
    /// <summary>
    /// If <see cref="desired"/> provided - attempts to set wagging to it, otherwise tries to toggle wagging.
    /// </summary>
    /// <param name="ent">The entity to toggle.</param>
    /// <param name="desired">The desired wagging state.</param>
    /// <returns>Whether wagging was toggled.</returns>
    [PublicAPI]
    public bool TryToggleWagging(Entity<WaggingComponent?> ent, bool? desired = null)
>>>>>>> theirs
    {
        if (!Resolve(uid, ref wagging, ref humanoid))
            return false;

<<<<<<< ours
        if (!humanoid.MarkingSet.Markings.TryGetValue(MarkingCategories.Tail, out var markings))
||||||| base
        if (!_visualBody.TryGatherMarkingsData(ent.Owner,
                [ent.Comp.Layer],
                out _,
                out _,
                out var applied))
        {
=======
        if (desired == ent.Comp.Wagging)
            return false;

        if (!_visualBody.TryGatherMarkingsData(ent.Owner,
                [ent.Comp.Layer],
                out _,
                out _,
                out var applied))
        {
>>>>>>> theirs
            return false;

        if (markings.Count == 0)
            return false;

<<<<<<< ours
        wagging.Wagging = !wagging.Wagging;
||||||| base
        ent.Comp.Wagging = !ent.Comp.Wagging;
=======
        ent.Comp.Wagging = !ent.Comp.Wagging;
        DirtyField(ent, nameof(WaggingComponent.Wagging));
>>>>>>> theirs

<<<<<<< ours
        for (var idx = 0; idx < markings.Count; idx++) // Animate all possible tails
||||||| base
        markingsSet = markingsSet.ShallowClone();
        foreach (var (layers, markings) in markingsSet)
=======
        markingsSet = markingsSet.ShallowClone();
        foreach (var (layers, _) in markingsSet)
>>>>>>> theirs
        {
            var currentMarkingId = markings[idx].MarkingId;
            string newMarkingId;

<<<<<<< ours
            if (wagging.Wagging)
||||||| base
            for (int i = 0; i < layerMarkings.Count; i++)
=======
            for (var i = 0; i < layerMarkings.Count; i++)
>>>>>>> theirs
            {
                newMarkingId = $"{currentMarkingId}{wagging.Suffix}";
            }
            else
            {
                if (currentMarkingId.EndsWith(wagging.Suffix))
                {
                    newMarkingId = currentMarkingId[..^wagging.Suffix.Length];
                }
                else
                {
                    if (currentMarkingId.Id.EndsWith(ent.Comp.Suffix))
                    {
                        newMarkingId = currentMarkingId.Id[..^ent.Comp.Suffix.Length];
                    }
                    else
                    {
                        newMarkingId = currentMarkingId;
                        Log.Warning($"Unable to revert wagging for {currentMarkingId}");
                    }
                }

                if (!ProtoMan.HasIndex<MarkingPrototype>(newMarkingId))
                {
                    Log.Warning($"{ToPrettyString(ent):ent} tried toggling wagging but {newMarkingId} marking doesn't exist");
                    continue;
                }
            }

            if (!_prototype.HasIndex<MarkingPrototype>(newMarkingId))
            {
                Log.Warning($"{ToPrettyString(uid)} tried toggling wagging but {newMarkingId} marking doesn't exist");
                continue;
            }

            _humanoidAppearance.SetMarkingId(uid, MarkingCategories.Tail, idx, newMarkingId,
                humanoid: humanoid);
        }

        return true;
    }
}
