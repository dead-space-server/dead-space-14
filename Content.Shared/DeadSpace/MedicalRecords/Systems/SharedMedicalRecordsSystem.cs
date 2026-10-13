// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.DeadSpace.MedicalRecords.Components;
using Content.Shared.StatusIcon;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.MedicalRecords.Systems;

/// <summary>
/// Shared base for the medical records record-mutation system, mirroring
/// <c>Content.Shared.CriminalRecords.Systems.SharedCriminalRecordsSystem</c>.
/// </summary>
public abstract class SharedMedicalRecordsSystem : EntitySystem
{
    [Dependency] private readonly ILogManager _logManager = default!;

    private ISawmill _sawmill = default!;

    public const string IconOnTreatment = "MedicalStatusIconOnTreatment";

    public const string IconPsychUnstable = "MedicalStatusIconPsychUnstable";

    public const string IconCompletedTreatment = "MedicalStatusIconCompletedTreatment";

    public const string IconForcedTreatment = "MedicalStatusIconForcedTreatment";

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("medical-records");
    }

    public static bool RequiresForcedTreatment(MedicalRecord? record)
    {
        if (record is null)
            return false;

        if (record.Status == MedicalStatus.PsychUnstable)
            return true;

        return record.History.Exists(medicalCase => medicalCase.NeedsForcedTreatment);
    }

    public static List<ProtoId<MedicalStatusIconPrototype>> GetStatusIcons(MedicalRecord record)
    {
        var icons = new List<ProtoId<MedicalStatusIconPrototype>>();

        switch (record.Status)
        {
            case MedicalStatus.OnTreatment:
                icons.Add(IconOnTreatment);
                break;
            case MedicalStatus.PsychUnstable:
                icons.Add(IconPsychUnstable);
                break;
            case MedicalStatus.CompletedTreatment:
                icons.Add(IconCompletedTreatment);
                break;
        }

        if (RequiresForcedTreatment(record))
            icons.Add(IconForcedTreatment);

        return icons;
    }

    public void SetMedicalIcons(string name, MedicalRecord? record)
    {
        var query = EntityQueryEnumerator<IdentityComponent>();
        var matched = false;

        while (query.MoveNext(out var uid, out _))
        {
            var visibleName = Identity.Name(uid, EntityManager);
            if (!visibleName.Equals(name))
                continue;

            matched = true;
            ApplyMedicalIcons(uid, record);
        }

        if (!matched)
            _sawmill.Warning($"SetMedicalIcons: no IdentityComponent entity matched record name '{name}' - status icons were not attached to anyone");
    }

    public void ApplyMedicalIcons(EntityUid characterUid, MedicalRecord? record)
    {
        var icons = record is null ? new List<ProtoId<MedicalStatusIconPrototype>>() : GetStatusIcons(record);

        if (icons.Count == 0)
        {
            RemComp<MedicalRecordComponent>(characterUid);
            return;
        }

        EnsureComp<MedicalRecordComponent>(characterUid, out var component);

        if (component.Icons.SequenceEqual(icons))
            return;

        component.Icons.Clear();
        component.Icons.AddRange(icons);
        Dirty(characterUid, component);
    }
}
