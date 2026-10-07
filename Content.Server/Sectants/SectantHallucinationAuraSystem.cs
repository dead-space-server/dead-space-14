using Content.Shared.NPC.Components;
using Content.Shared.Sectants;
using Content.Shared.StatusEffect;
using Robust.Shared.Timing;

namespace Content.Server.Sectants;

public sealed class SectantHallucinationAuraSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly HashSet<EntityUid> _entitySet = new();

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<SectantHallucinationAuraComponent>();
        while (query.MoveNext(out var uid, out var aura))
        {
            if (_timing.CurTime < aura.NextTick) continue;
            aura.NextTick = _timing.CurTime + TimeSpan.FromSeconds(aura.TickInterval);

            var coords = Transform(uid).Coordinates;
            _entitySet.Clear();
            _lookup.GetEntitiesInRange(coords, aura.Radius, _entitySet);

            foreach (var target in _entitySet)
            {
                if (target == uid) continue;
                if (!HasComp<StatusEffectsComponent>(target)) continue;

                // Пропускаем союзников — не накладываем дебаффы на своих.
                if (AreInSameFaction(uid, target)) continue;

                _status.TryAddStatusEffect(target, aura.SleepEffect,  TimeSpan.FromSeconds(3), refresh: true);
                _status.TryAddStatusEffect(target, aura.PacifyEffect, TimeSpan.FromSeconds(5), refresh: true);
                _status.TryAddStatusEffect(target, aura.BlindEffect,  TimeSpan.FromSeconds(4), refresh: true);
            }
        }
    }

    /// <summary>
    /// Ручная проверка общих фракций — обходит RA0002 (прямой доступ к Factions разрешён только на чтение).
    /// </summary>
    private bool AreInSameFaction(EntityUid a, EntityUid b)
    {
        if (!TryComp<NpcFactionMemberComponent>(a, out var ca)) return false;
        if (!TryComp<NpcFactionMemberComponent>(b, out var cb)) return false;

        foreach (var fa in ca.Factions)
        foreach (var fb in cb.Factions)
            if (fa == fb) return true;

        return false;
    }
}
