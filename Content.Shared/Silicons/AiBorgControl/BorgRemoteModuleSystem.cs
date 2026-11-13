using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;

namespace Content.Shared.Silicons.AiBorgControl;

public sealed partial class BorgRemoteModuleSystem : EntitySystem
{
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BorgRemoteModuleComponent, ContainerGettingInsertedAttemptEvent>(OnInsertAttempt);
    }

    private void OnInsertAttempt(EntityUid uid, BorgRemoteModuleComponent comp, ContainerGettingInsertedAttemptEvent args)
    {
        var chassisUid = args.Container.Owner;
        if (!TryComp<BorgChassisComponent>(chassisUid, out var chassis)
            || args.Container.ID != chassis.BrainContainerId)
            return;

        if (_whitelist.IsWhitelistFail(comp.ChassisWhitelist, chassisUid)
            || _whitelist.IsWhitelistPass(comp.ChassisBlacklist, chassisUid))
        {
            args.Cancel();
        }
    }
}
