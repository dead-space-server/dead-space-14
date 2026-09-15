using Content.Shared.Forensics.Components;
using Content.Shared.Inventory;

namespace Content.Shared.Forensics.Systems;

public sealed partial class FingerprintMaskSystem : EntitySystem
{
    // DS14-start: current engine uses explicit event subscriptions.
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FingerprintMaskComponent, InventoryRelayedEvent<TryAccessFingerprintEvent>>(OnTryAccessFingerprint);
    }
    // DS14-end

    private void OnTryAccessFingerprint(Entity<FingerprintMaskComponent> gloves, ref InventoryRelayedEvent<TryAccessFingerprintEvent> args)
    {
        if (args.Args.Cancelled)
            return;

        args.Args.Blocker = gloves.Owner;
        args.Args.Cancel();
    }
}
