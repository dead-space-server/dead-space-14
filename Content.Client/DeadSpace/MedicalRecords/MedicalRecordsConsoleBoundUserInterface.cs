// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.MedicalRecords;
using Content.Shared.DeadSpace.MedicalRecords.Components;
using Content.Shared.StationRecords;

namespace Content.Client.DeadSpace.MedicalRecords;

public sealed class MedicalRecordsConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private MedicalRecordsConsoleWindow? _window;

    public MedicalRecordsConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        var comp = EntMan.GetComponent<MedicalRecordsConsoleComponent>(Owner);

        _window = new(comp.MaxStringLength);
        _window.OnKeySelected += key => SendMessage(new SelectStationRecord(key));
        _window.OnFiltersChanged += (type, value) => SendMessage(new SetStationRecordFilter(type, value));
        _window.OnStatusFilterPressed += status => SendMessage(new MedicalRecordSetStatusFilter(status));
        _window.OnStatusChange += status => SendMessage(new MedicalRecordChangeStatus(status));

        _window.OnCaseAdded += draft => SendMessage(new MedicalRecordAddCase(
            draft.AdmissionState,
            draft.Diagnosis,
            draft.Treatment,
            draft.NeedsContinuedTreatment,
            draft.NeedsForcedTreatment,
            draft.Specialists,
            draft.Recommendations,
            draft.DischargeState));

        _window.OnCaseEdited += (index, draft) => SendMessage(new MedicalRecordEditCase(
            index,
            draft.AdmissionState,
            draft.Diagnosis,
            draft.Treatment,
            draft.NeedsContinuedTreatment,
            draft.NeedsForcedTreatment,
            draft.Specialists,
            draft.Recommendations,
            draft.DischargeState,
            draft.Open));

        _window.OnCaseDeleted += index => SendMessage(new MedicalRecordDeleteCase(index));
        _window.OnCasePrinted += index => SendMessage(new MedicalRecordPrintCase(index));
        _window.OnClose += Close;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not MedicalRecordsConsoleState cast)
            return;

        _window?.UpdateState(cast);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        _window?.Close();
    }
}
