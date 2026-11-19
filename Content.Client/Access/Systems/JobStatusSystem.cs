using Content.Client.Overlays;
using Content.Shared.Access.Systems;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Access.Systems;

public sealed partial class JobStatusSystem : SharedJobStatusSystem
{
    [Dependency] private ShowJobIconsSystem _showJobIcons = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<JobStatusComponent, GetStatusIconsEvent>(OnGetStatusIconsEvent);
    }

    // show the status icons if the player has the correponding HUDs
    private void OnGetStatusIconsEvent(Entity<JobStatusComponent> ent, ref GetStatusIconsEvent ev)
    {
        if (_showJobIcons.IsActive && ent.Comp.JobStatusIcon != null)
            ev.StatusIcons.Add(_prototype.Index(ent.Comp.JobStatusIcon));
    }
}
