// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Destructible;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Temperature;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;

namespace Content.Server.DeadSpace.Temperature;

public sealed class CorpseBurningSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CorpseBurningComponent, DamageChangedEvent>(OnDamageChanged,
            before: [typeof(DestructibleSystem)], after: [typeof(MobThresholdSystem)]);
        SubscribeLocalEvent<CorpseBurningComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<CorpseBurningComponent, ExaminedEvent>(OnExamined);
    }

    private void OnDamageChanged(Entity<CorpseBurningComponent> ent, ref DamageChangedEvent args)
    {
        var heat = args.Damageable.Damage.DamageDict.GetValueOrDefault("Heat");
        var suppressFresh = heat >= CorpseBurningComponent.FirstStageDamage ||
                            ent.Comp.SuppressFreshDescription && heat > 0;
        if (ent.Comp.SuppressFreshDescription != suppressFresh)
        {
            ent.Comp.SuppressFreshDescription = suppressFresh;
            Dirty(ent, ent.Comp);
        }

        if (args.DamageDelta != null &&
            (!args.DamageDelta.DamageDict.TryGetValue("Heat", out var addedHeat) || addedHeat <= 0))
            return;

        AnnounceStage(ent, args.Damageable);
    }

    private void OnMobStateChanged(Entity<CorpseBurningComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead && TryComp<DamageableComponent>(ent, out var damage))
            AnnounceStage(ent, damage);
    }

    private void AnnounceStage(Entity<CorpseBurningComponent> ent, DamageableComponent damage)
    {
        if (!_mobState.IsDead(ent) || TerminatingOrDeleted(ent) || EntityManager.IsQueuedForDeletion(ent))
            return;

        var stage = GetStage(damage);
        if (stage <= ent.Comp.LastPopupStage)
            return;

        // A large hit announces only the latest stage, instead of stacking every skipped popup.
        ent.Comp.LastPopupStage = stage;
        _popup.PopupEntity(Loc.GetString($"corpse-burning-{ent.Comp.MessageType}-popup-{stage}"),
            ent, PopupType.MediumCaution);
    }

    private void OnExamined(Entity<CorpseBurningComponent> ent, ref ExaminedEvent args)
    {
        if (!_mobState.IsDead(ent) || !TryComp<DamageableComponent>(ent, out var damage))
            return;

        var stage = GetStage(damage);
        if (stage > 0)
            args.PushMarkup(Loc.GetString($"corpse-burning-{ent.Comp.MessageType}-examine-{stage}"));
    }

    private static int GetStage(DamageableComponent damage)
    {
        var heat = damage.Damage.DamageDict.GetValueOrDefault("Heat", FixedPoint2.Zero);
        // Ordinary bodies turn to ash at 1500 Heat, leaving 400 damage after the final warning.
        return heat >= 1100 ? 5 : heat >= 950 ? 4 : heat >= 800 ? 3 : heat >= 600 ? 2
            : heat >= CorpseBurningComponent.FirstStageDamage ? 1 : 0;
    }
}
