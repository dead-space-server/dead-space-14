// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage.Systems;
using Content.Shared.Hands;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Throwing;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Lavaland;

/// <summary>
/// Hidden out of reality by the immortality talisman: untouchable, and touching nothing either - no shot, no blow,
/// no item used, picked up, dropped, thrown or put on from inside the hole.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LavalandRealityHiddenComponent : Component;

public sealed class LavalandRealityHiddenSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandRealityHiddenComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<LavalandRealityHiddenComponent, InteractionAttemptEvent>(OnInteractionAttempt);
        SubscribeLocalEvent<LavalandRealityHiddenComponent, UseAttemptEvent>(OnCancellable);
        SubscribeLocalEvent<LavalandRealityHiddenComponent, PickupAttemptEvent>(OnCancellable);
        SubscribeLocalEvent<LavalandRealityHiddenComponent, DropAttemptEvent>(OnCancellable);
        SubscribeLocalEvent<LavalandRealityHiddenComponent, ThrowAttemptEvent>(OnCancellable);
        SubscribeLocalEvent<LavalandRealityHiddenComponent, IsEquippingAttemptEvent>(OnCancellable);
        SubscribeLocalEvent<LavalandRealityHiddenComponent, IsUnequippingAttemptEvent>(OnCancellable);
        // Untouchable, but with its wounds kept as they were: no health shown full while out, as godmode did.
        SubscribeLocalEvent<LavalandRealityHiddenComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
    }

    private void OnAttackAttempt(Entity<LavalandRealityHiddenComponent> ent, ref AttackAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnInteractionAttempt(Entity<LavalandRealityHiddenComponent> ent, ref InteractionAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnCancellable(EntityUid uid, LavalandRealityHiddenComponent comp, CancellableEntityEventArgs args)
    {
        args.Cancel();
    }

    private void OnBeforeDamage(Entity<LavalandRealityHiddenComponent> ent, ref BeforeDamageChangedEvent args)
    {
        args.Cancelled = true;
    }
}
