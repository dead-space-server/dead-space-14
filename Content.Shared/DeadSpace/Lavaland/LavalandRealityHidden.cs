// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Interaction.Events;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Lavaland;

/// <summary>
/// Hidden out of reality by the immortality talisman: untouchable, and touching nothing either - no shot, no blow
/// from inside the hole.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LavalandRealityHiddenComponent : Component;

public sealed class LavalandRealityHiddenSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandRealityHiddenComponent, AttackAttemptEvent>(OnAttackAttempt);
    }

    private void OnAttackAttempt(Entity<LavalandRealityHiddenComponent> ent, ref AttackAttemptEvent args)
    {
        args.Cancel();
    }
}
