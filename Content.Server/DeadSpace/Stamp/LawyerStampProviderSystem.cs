// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.GameTicking;
using Content.Server.Inventory;
using Content.Shared.Paper;

namespace Content.Server.DeadSpace.Stamp;

public sealed partial class LawyerStampProviderSystem : EntitySystem
{
    [Dependency] private ServerInventorySystem _inventory = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<LawyerStampProviderComponent, PlayerSpawnCompleteEvent>(OnSpawn);
        SubscribeLocalEvent<LawyerStampProviderComponent, MapInitEvent>(OnTakeGhostRole); // TODO: check why there two events
    }

    private void OnSpawn(EntityUid uid, LawyerStampProviderComponent comp, PlayerSpawnCompleteEvent args)
    {
        var stamp = Spawn(comp.StampPrototype, Transform(args.Mob).Coordinates);

        if (!TrySetupLawyerStamp(stamp, args.Profile.Name)) // DS14
            return;

        _inventory.TryEquip(uid, stamp, comp.Slot, true, true); // DS14
    }

    private void OnTakeGhostRole(EntityUid uid, LawyerStampProviderComponent comp, MapInitEvent args)
    {
        var stamp = Spawn(comp.StampPrototype, Transform(uid).Coordinates);
        var name = MetaData(uid).EntityName; // DS14

        if (!TrySetupLawyerStamp(stamp, name)) // DS14
            return;

        _inventory.TryEquip(uid, stamp, comp.Slot, true, true); // DS14
    }

    // DS14-start
    private bool TrySetupLawyerStamp(EntityUid stamp, string ownerName)
    {
        if (!TryComp<StampComponent>(stamp, out var stampComp))
            return false;

        var lawyerText = Loc.GetString("stamp-component-stamped-name-lawyer");
        stampComp.StampedName = $"{lawyerText} {ownerName}";
        stampComp.StampMainText = ownerName;
        Dirty(stamp, stampComp);

        return true;
    }
    // DS14-end
}
