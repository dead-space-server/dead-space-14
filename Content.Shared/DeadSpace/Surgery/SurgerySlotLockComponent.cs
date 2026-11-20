// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Inventory.Events;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Surgery;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurgerySlotLockComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<string> Slots = new();
}

public sealed class SurgerySlotLockSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgerySlotLockComponent, IsEquippingTargetAttemptEvent>(OnEquip);
    }

    private void OnEquip(Entity<SurgerySlotLockComponent> ent, ref IsEquippingTargetAttemptEvent args)
    {
        if (!ent.Comp.Slots.Contains(args.Slot))
            return;

        args.Reason = "surgery-slot-locked";
        args.Cancel();
    }
}
