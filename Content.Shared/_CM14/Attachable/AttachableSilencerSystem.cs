using Content.Shared._CM14.Attachable.Components;
using Content.Shared._CM14.Attachable.Events;
using Content.Shared.Weapons.Ranged.Events;
namespace Content.Shared._CM14.Attachable;
public sealed class AttachableSilencerSystem : EntitySystem
{
    public override void Initialize()
    {
        // DS14-start
        SubscribeLocalEvent<AttachableSilencerComponent, AttachableRelayedEvent<GunRefreshModifiersEvent>>(OnSilencerRefreshModifiers);
        SubscribeLocalEvent<AttachableSilencerComponent, AttachableRelayedEvent<GunMuzzleFlashAttemptEvent>>(OnSilencerMuzzleFlash);
        // DS14-end
    }
    // DS14-start
    private void OnSilencerRefreshModifiers(Entity<AttachableSilencerComponent> ent, ref AttachableRelayedEvent<GunRefreshModifiersEvent> args)
    {
        args.Args.SoundGunshot = ent.Comp.Sound;
    }
    private void OnSilencerMuzzleFlash(Entity<AttachableSilencerComponent> ent, ref AttachableRelayedEvent<GunMuzzleFlashAttemptEvent> args)
    {
        args.Args.Cancelled = true;
    }
    // DS14-end
}
