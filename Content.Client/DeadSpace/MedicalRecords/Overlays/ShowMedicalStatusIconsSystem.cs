// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Overlays;
using Content.Shared.DeadSpace.MedicalRecords.Components;
using Content.Shared.DeadSpace.MedicalRecords.Overlays;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.MedicalRecords.Overlays;

/// <summary>
/// Renders the medical status set on the HUD: the full set on a medical HUD, and the
/// security-visible subset on a security-only HUD. Mirrors
/// <c>Content.Client.DeadSpace.PersonnelRecords.Overlays.ShowPersonnelRecordIconsSystem</c>.
/// </summary>
public sealed class ShowMedicalStatusIconsSystem : EquipmentHudSystem<ShowMedicalStatusIconsComponent>
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    [Dependency] private readonly ShowSecurityMedicalStatusIconsSystem _security = default!;

    [Dependency] private readonly ILogManager _logManager = default!;

    [Dependency] private readonly IGameTiming _timing = default!;

    private ISawmill _sawmill = default!;
    private TimeSpan _nextGateLog;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("medical-records-icons");

        SubscribeLocalEvent<MedicalRecordComponent, GetStatusIconsEvent>(OnGetStatusIcons);
    }

    private void OnGetStatusIcons(Entity<MedicalRecordComponent> ent, ref GetStatusIconsEvent ev)
    {
        if (ent.Comp.Icons.Count > 0 && _timing.CurTime >= _nextGateLog)
        {
            _nextGateLog = _timing.CurTime + TimeSpan.FromSeconds(2);
            _sawmill.Info($"GATE: hud={IsActive} sec={_security.IsActive} icons=[{string.Join(", ", ent.Comp.Icons)}]");
        }

        if (IsActive)
        {
            foreach (var icon in ent.Comp.Icons)
            {
                if (_prototype.Resolve(icon, out MedicalStatusIconPrototype? iconPrototype))
                    ev.StatusIcons.Add(iconPrototype);
            }

            return;
        }

        // A medical HUD already draws everything the record implies, so a combined medsec HUD -
        // which is active on both layers - gets the full set exactly once instead of the shared
        // icons being stacked underneath themselves.
        if (!_security.IsActive)
            return;

        foreach (var icon in ent.Comp.Icons)
        {
            if (_prototype.Resolve(icon, out MedicalStatusIconPrototype? iconPrototype)
                && iconPrototype.SecurityVisible)
            {
                ev.StatusIcons.Add(iconPrototype);
            }
        }
    }
}
