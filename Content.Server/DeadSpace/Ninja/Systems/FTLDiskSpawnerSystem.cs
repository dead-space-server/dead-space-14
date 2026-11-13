using Content.Shared.Shuttles.Components;
using Content.Server.DeadSpace.Ninja.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Labels.EntitySystems;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed partial class FTLDiskSpawnerSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private LabelSystem _labelSystem = default!;
    [Dependency] private SharedStorageSystem _storageSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FTLDiskSpawnerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, FTLDiskSpawnerComponent component, MapInitEvent args)
    {
        var transform = Transform(uid);

        if (!_mapSystem.TryGetMap(transform.MapID, out var map))
            return;

        var disk = SpawnAtPosition(component.DiskPrototype, transform.Coordinates);

        var diskComponent = EnsureComp<ShuttleDestinationCoordinatesComponent>(disk);

        diskComponent.Destination = map.Value;

        Dirty(disk, diskComponent);

        var diskCase = SpawnAtPosition(component.CasePrototype, transform.Coordinates);

        if (TryComp<MetaDataComponent>(diskComponent.Destination, out var meta) && meta != null && meta.EntityName != null)
        {
            _labelSystem.Label(disk, meta.EntityName);
            _labelSystem.Label(diskCase, meta.EntityName);
        }

        _storageSystem.Insert(diskCase, disk, out _, playSound: false);
    }
}