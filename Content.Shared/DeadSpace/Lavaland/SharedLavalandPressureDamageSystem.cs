// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Wieldable;

namespace Content.Shared.DeadSpace.Lavaland;

/// <summary>
/// The melee side of <see cref="LavalandPressureDamageComponent"/>, here so the client predicts the weakened blow too:
/// only the server knows the air, and it keeps <see cref="LavalandPressureDamageComponent.Weakened"/> up to date.
/// </summary>
public abstract class SharedLavalandPressureDamageSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LavalandPressureDamageComponent, GetMeleeDamageEvent>(OnGetMeleeDamage,
            after: new[] { typeof(SharedWieldableSystem) });
    }

    /// <summary>The server checks the air around the weapon again; the client takes what it was last told.</summary>
    protected virtual void RefreshWeakened(Entity<LavalandPressureDamageComponent> ent)
    {
    }

    private void OnGetMeleeDamage(Entity<LavalandPressureDamageComponent> ent, ref GetMeleeDamageEvent args)
    {
        if (!ent.Comp.ApplyToMelee)
            return;

        RefreshWeakened(ent);
        if (ent.Comp.Weakened)
            args.Damage *= ent.Comp.OutOfRangeModifier;
    }
}
