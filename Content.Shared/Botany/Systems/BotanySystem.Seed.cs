using JetBrains.Annotations;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Items.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Cloning;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Random;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Botany.Systems;

public sealed partial class BotanySystem : EntitySystem
{
    // DS14-start: current engine uses explicit event subscriptions.
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ProduceComponent, ExaminedEvent>(OnProduceExamined);
        SubscribeLocalEvent<SeedComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<SeedComponent, ComponentShutdown>(OnSeedShutdown);
        SubscribeLocalEvent<ProduceComponent, ComponentShutdown>(OnProduceShutdown);
        SubscribeLocalEvent<BotanySwabComponent, ComponentShutdown>(OnSwabShutdown);
    }
    // DS14-end

    // DS14-start: current engine uses readonly IoC fields.
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly PlantSystem _plant = default!;
    [Dependency] private readonly RandomHelperSystem _randomHelper = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedCloningSystem _cloning = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    // DS14-end

    public readonly ProtoId<CloningSettingsPrototype> SettingsId = "PlantClone";
    public readonly ProtoId<CloningSettingsPrototype> LifecycleSettingsId = "PlantLifecycleClone";

    private void OnExamined(Entity<SeedComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (!TryGetPlantComponent<PlantComponent>(ent.Comp.PlantData, ent.Comp.PlantProtoId, out var plant)
            || !TryGetPlantComponent<PlantDataComponent>(ent.Comp.PlantData, ent.Comp.PlantProtoId, out var plantData))
            return;

        using (args.PushGroup(nameof(SeedComponent), 1))
        {
            var name = Loc.GetString(plantData.Name);
            args.PushMarkup(Loc.GetString("seed-component-description", ("seedName", name)));
            args.PushMarkup(_plant.GetPlantStateMarkup(ent.Owner, plant));
        }
    }

    private void OnSeedShutdown(Entity<SeedComponent> ent, ref ComponentShutdown args)
    {
        DeletePlantSnapshot(ent.Comp.PlantData);
    }

    private void OnProduceShutdown(Entity<ProduceComponent> ent, ref ComponentShutdown args)
    {
        DeletePlantSnapshot(ent.Comp.PlantData);
    }

    private void OnSwabShutdown(Entity<BotanySwabComponent> ent, ref ComponentShutdown args)
    {
        DeletePlantSnapshot(ent.Comp.PlantData);
    }

    /// <summary>
    /// Tries to get a plant component from a snapshot or prototype.
    /// </summary>
    /// <typeparam name="T">The type of component to get.</typeparam>
    /// <param name="snapshot">The snapshot to get the component from.</param>
    /// <param name="plantProtoId">The prototype ID to get the component from.</param>
    /// <param name="plant">The plant component if found.</param>
    [PublicAPI]
    public bool TryGetPlantComponent<T>(EntityUid? snapshot, EntProtoId? plantProtoId, [NotNullWhen(true)] out T? plant)
        where T : class, IComponent, new()
    {
        plant = null;

        if (snapshot != null && TryComp(snapshot, out plant))
            return true;

        if (plantProtoId == null)
            return false;

        if (!_prototypeManager.TryIndex(plantProtoId.Value, out var proto)) // DS14 - current engine has no EntitySystem.ProtoMan shortcut.
            return false;

        return proto.TryGetComponent(out plant, _componentFactory); // DS14 - use the current EntityPrototype component API.
    }

    /// <summary>
    /// Clones a component snapshot of a plant.
    /// </summary>
    /// <param name="source">The entity to clone the snapshot from.</param>
    /// <param name="parent">The entity that should own the snapshot, if any.</param>
    /// <param name="cloneLifecycle">If true, also clone lifecycle state into the snapshot.</param>
    [PublicAPI]
    public EntityUid? ClonePlantSnapshotData(EntityUid source, EntityUid? parent = null, bool cloneLifecycle = false)
    {
        var settingsId = cloneLifecycle ? LifecycleSettingsId : SettingsId;
        if (!_prototypeManager.TryIndex(settingsId, out var settings)) // DS14 - current engine has no EntitySystem.ProtoMan shortcut.
            return null;

        var snapshot = EntityManager.CreateEntityUninitialized(null);
        _cloning.CloneComponents(source, snapshot, settings);
        EntityManager.InitializeAndStartEntity(snapshot, doMapInit: false);

        if (parent is { } parentUid)
            _transform.SetParent(snapshot, parentUid);

        return snapshot;
    }

    /// <summary>
    /// Deletes a stored plant snapshot, if one exists.
    /// </summary>
    [PublicAPI]
    public void DeletePlantSnapshot(EntityUid? snapshot)
    {
        if (snapshot == null)
            return;

        PredictedQueueDel(snapshot.Value);
    }

    /// <summary>
    /// Applies the component data stored in a plant snapshot to a target entity.
    /// </summary>
    /// <param name="snapshot">The snapshot entity to copy component data from.</param>
    /// <param name="target">The entity to apply the snapshot data to.</param>
    /// <param name="cloneLifecycle">If true, also copy lifecycle state.</param>
    [PublicAPI]
    public void ApplyPlantSnapshotData(EntityUid? snapshot, EntityUid target, bool cloneLifecycle = false)
    {
        if (snapshot == null)
            return;

        var settingsId = cloneLifecycle ? LifecycleSettingsId : SettingsId;
        if (!_prototypeManager.TryIndex(settingsId, out var settings)) // DS14 - current engine has no EntitySystem.ProtoMan shortcut.
            return;

        _cloning.CloneComponents(snapshot.Value, target, settings);
    }

    /// <summary>
    /// Internal method to spawn a seed packet from a plant component.
    /// </summary>
    /// <param name="plantData">The plant data component to spawn.</param>
    /// <param name="plantProtoId">The plant prototype ID to store in the seed component.</param>
    /// <param name="snapshot">The component snapshot to store in the seed component.</param>
    /// <param name="coords">The coordinates to spawn the seed packet at.</param>
    /// <param name="user">The user who is spawning the seed packet.</param>
    /// <param name="healthOverride">The health override to store in the seed component.</param>
    /// <returns>The spawned seed packet entity.</returns>
    [PublicAPI]
    public EntityUid SpawnSeedPacket(
        PlantDataComponent plantData,
        EntProtoId plantProtoId,
        EntityUid? snapshot,
        EntityCoordinates coords,
        EntityUid user,
        float? healthOverride = null)
    {
        var seedItem = PredictedSpawnAtPosition(plantData.PacketPrototype, coords);
        var seedComp = EnsureComp<SeedComponent>(seedItem);
        seedComp.PlantProtoId = plantProtoId;
        seedComp.PlantData = snapshot.HasValue
            ? ClonePlantSnapshotData(snapshot.Value, parent: seedItem)
            : null;
        seedComp.HealthOverride = healthOverride;
        Dirty(seedItem, seedComp);

        var name = Loc.GetString(plantData.Name);
        var noun = Loc.GetString(plantData.Noun);
        _metaData.SetEntityName(seedItem, Loc.GetString("botany-seed-packet-name", ("seedName", name), ("seedNoun", noun)));

        _hands.TryPickupAnyHand(user, seedItem);
        return seedItem;
    }
}
