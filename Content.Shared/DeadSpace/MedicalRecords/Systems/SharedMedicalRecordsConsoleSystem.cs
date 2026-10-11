// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.IdentityManagement;
using Content.Shared.DeadSpace.MedicalRecords.Components;
using Content.Shared.Station;
using Content.Shared.StationRecords;
using Robust.Shared.GameObjects;

namespace Content.Shared.DeadSpace.MedicalRecords.Systems;

/// <summary>
/// Shared base for the Medical Records console system, mirroring
/// <c>Content.Shared.CriminalRecords.Systems.SharedCriminalRecordsConsoleSystem</c>.
/// </summary>
public abstract class SharedMedicalRecordsConsoleSystem : EntitySystem
{
    [Dependency] private readonly SharedMedicalRecordsSystem _medicalRecords = default!;
    [Dependency] private readonly SharedStationRecordsSystem _records = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MetaDataComponent, IdentityChangedEvent>(OnIdentityChanged);
    }

    private void OnIdentityChanged(Entity<MetaDataComponent> ent, ref IdentityChangedEvent args)
    {
        CheckNewIdentity(ent);
    }

    public void CheckNewIdentity(EntityUid uid)
    {
        var name = Identity.Name(uid, EntityManager);
        var xform = Transform(uid);

        var station = _station.GetStationInMap(xform.MapID);

        if (station != null && _records.GetRecordByName(station.Value, name) is { } id)
        {
            _records.TryGetRecord<MedicalRecord>(new StationRecordKey(id, station.Value), out var record);
            _medicalRecords.ApplyMedicalIcons(uid, record);
            return;
        }

        RemComp<MedicalRecordComponent>(uid);
    }
}
