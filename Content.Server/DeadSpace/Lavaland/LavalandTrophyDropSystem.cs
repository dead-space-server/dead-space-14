// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Attachments;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// Crusher trophies (tgstation): a creature drops its trophy when killed mostly with a trophy-hunting weapon.
/// Fauna (<see cref="LavalandTrophyDropComponent.Proportional"/>): the chance scales with how much of its health
/// that weapon took, reaching <see cref="LavalandTrophyDropComponent.Chance"/> at <see cref="LavalandTrophyDropComponent.RequiredFraction"/>.
/// Megafauna: the full chance once the weapon dealt at least that fraction, nothing below it.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandTrophyDropComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Trophy;

    [DataField]
    public float Chance = 0.25f;

    [DataField]
    public float RequiredFraction = 1f;

    [DataField]
    public bool Proportional = true;

    [ViewVariables]
    public float HunterDamage;

    /// <summary>The next damage change comes from a trophy-hunting melee hit.</summary>
    [ViewVariables]
    public bool PendingHunterHit;

    [ViewVariables]
    public bool Dropped;
}

public sealed class LavalandTrophyDropSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LavalandTrophyDropComponent, AttackedEvent>(OnAttacked);
        SubscribeLocalEvent<LavalandTrophyDropComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<LavalandTrophyDropComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnAttacked(Entity<LavalandTrophyDropComponent> ent, ref AttackedEvent args)
    {
        ent.Comp.PendingHunterHit = TryComp<AttachmentHolderComponent>(args.Used, out var holder) && holder.TrophyHunter;
    }

    private void OnDamageChanged(Entity<LavalandTrophyDropComponent> ent, ref DamageChangedEvent args)
    {
        if (!ent.Comp.PendingHunterHit)
            return;

        ent.Comp.PendingHunterHit = false;
        if (args.DamageIncreased && args.DamageDelta != null)
            ent.Comp.HunterDamage += args.DamageDelta.GetTotal().Float();
    }

    private void OnMobStateChanged(Entity<LavalandTrophyDropComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || ent.Comp.Dropped || ent.Comp.HunterDamage <= 0f ||
            !_thresholds.TryGetDeadThreshold(ent, out var threshold) || threshold.Value <= 0)
        {
            return;
        }

        var fraction = ent.Comp.HunterDamage / threshold.Value.Float();
        var required = Math.Max(0.01f, ent.Comp.RequiredFraction);
        var chance = ent.Comp.Proportional
            ? ent.Comp.Chance * Math.Min(1f, fraction / required)
            : fraction >= required ? ent.Comp.Chance : 0f;
        ent.Comp.Dropped = true;
        if (!_random.Prob(Math.Clamp(chance, 0f, 1f)))
            return;

        Spawn(ent.Comp.Trophy, _transform.GetMapCoordinates(ent));
    }
}
