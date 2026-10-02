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

        // IdentitySystem.UpdateIdentityInfo raises IdentityChangedEvent directed at the character
        // entity right before its own criminal-records call - subscribing to that existing event is
        // the same "re-check the icon when the name changes" hook Criminal Records gets, without
        // touching IdentitySystem.cs. Matters here because the medical record is keyed by name and
        // a chameleon or a mind-swap would otherwise keep the previous occupant's syringe icon.
        //
        // Keyed on MetaDataComponent rather than IdentityComponent on purpose: the event bus allows
        // exactly one subscription per (component, event) pair per side, and the Dead Space
        // Personnel Records console already owns (IdentityComponent, IdentityChangedEvent) on the
        // server. The character always carries MetaDataComponent, and IdentityChangedEvent is only
        // ever raised directed at a character, so this pair is equivalent and does not collide.
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
