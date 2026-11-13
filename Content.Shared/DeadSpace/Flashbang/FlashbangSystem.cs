// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Flash;
using Content.Shared.Flash.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Movement.Systems;
using Content.Shared.Random.Helpers;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Content.Shared.Timing;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Components.Effects;
using Content.Shared.Examine;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.DeadSpace.Flashbang;

public sealed partial class FlashbangSystem : XOnTriggerSystem<FlashbangComponent>
{
    [Dependency] private EntityLookupSystem _entityLookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ExamineSystemShared _examine = default!;

    private readonly HashSet<EntityUid> _entSet = new();

    private EntityQuery<StatusEffectsComponent> _statusQuery;
    private EntityQuery<DamagedByFlashingComponent> _damagedQuery;
    private EntityQuery<FlashbangProtectionComponent> _flashbangProtQuery;

    public override void Initialize()
    {
        base.Initialize();
        _statusQuery = GetEntityQuery<StatusEffectsComponent>();
        _damagedQuery = GetEntityQuery<DamagedByFlashingComponent>();
        _flashbangProtQuery = GetEntityQuery<FlashbangProtectionComponent>();
    }

    protected override void OnTrigger(Entity<FlashbangComponent> ent, EntityUid target, ref TriggerEvent args)
    {
        var component = ent.Comp;
        var transform = Transform(target);
        var mapPosition = _transform.GetMapCoordinates(transform);
        var maxRange = Math.Max(component.KnockdownRange, component.StunRange);

        _entSet.Clear();
        _entityLookup.GetEntitiesInRange(transform.Coordinates, maxRange, _entSet);

        foreach (var entity in _entSet)
        {
            if (!SharedRandomExtensions.PredictedProb(_timing, component.Probability, GetNetEntity(entity)))
                continue;

            if (!_statusQuery.HasComponent(entity) && !_damagedQuery.HasComponent(entity))
                continue;

            var entityPos = _transform.GetMapCoordinates(entity);
            var distance = (entityPos.Position - mapPosition.Position).Length();

            float protection = 0f;

            if (_inventory.TryGetSlotEntity(entity, "head", out var headEntity))
            {
                if (_flashbangProtQuery.TryGetComponent(headEntity, out var protComp))
                    protection += protComp.Reduction;
            }

            if (_inventory.TryGetSlotEntity(entity, "ears", out var earsEntity))
            {
                if (_flashbangProtQuery.TryGetComponent(earsEntity, out var protComp))
                    protection += protComp.Reduction;
            }

            if (_inventory.TryGetSlotEntity(entity, "mask", out var maskEntity))
            {
                if (_flashbangProtQuery.TryGetComponent(maskEntity, out var protComp))
                    protection += protComp.Reduction;
            }

            var effectiveStunRange = Math.Max(0, component.StunRange - protection);
            var effectiveKnockdownRange = Math.Max(0, component.KnockdownRange - protection);

            if ((effectiveStunRange <= 0 || distance > effectiveStunRange) &&
                (effectiveKnockdownRange <= 0 || distance > effectiveKnockdownRange))
                continue;

            if (!_examine.InRangeUnOccluded(entity, mapPosition, maxRange, predicate: (e) => _damagedQuery.HasComponent(e)))
                continue;

            if (effectiveStunRange > 0 && distance <= effectiveStunRange)
            {
                _stun.TryAddStunDuration(entity, component.StunDuration);
            }

            if (effectiveKnockdownRange > 0 && distance <= effectiveKnockdownRange)
            {
                _stun.TryKnockdown(entity, component.KnockdownDuration, refresh: true);
            }
        }

        args.Handled = true;
    }
}
