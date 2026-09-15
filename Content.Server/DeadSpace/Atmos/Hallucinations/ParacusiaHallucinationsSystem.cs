// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Traits.Assorted;
using Content.Shared.Humanoid;
using Content.Shared.StatusEffectNew;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Atmos.Hallucinations;

public sealed class ParacusiaHallucinationsSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ParacusiaSystem _paracusia = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;

    public static readonly EntProtoId StatusEffectParacusia = "StatusEffectParacusia";

    private static readonly SoundSpecifier HallucinationSounds = new SoundCollectionSpecifier("Paracusia");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ParacusiaStatusEffectComponent, StatusEffectAppliedEvent>(OnStatusApplied);
        SubscribeLocalEvent<ParacusiaStatusEffectComponent, StatusEffectRemovedEvent>(OnStatusRemoved);
    }

    private void OnStatusApplied(Entity<ParacusiaStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        var paracusia = EnsureComp<ParacusiaComponent>(args.Target);
        _paracusia.SetSounds(args.Target, HallucinationSounds, paracusia);
        _paracusia.SetTime(args.Target, 2f, 8f, paracusia);
        _paracusia.SetDistance(args.Target, 5f, paracusia);
    }

    private void OnStatusRemoved(Entity<ParacusiaStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        RemComp<ParacusiaComponent>(args.Target);
    }

    public void CauseHallucinationsInRange(MapCoordinates coords, float range, TimeSpan duration)
    {
        if (coords.MapId == MapId.Nullspace)
            return;

        foreach (var ent in _lookup.GetEntitiesInRange<HumanoidAppearanceComponent>(coords, range))
        {
            if (HasComp<ParacusiaComponent>(ent))
                continue;

            _statusEffects.TryAddStatusEffectDuration(ent, StatusEffectParacusia, duration);
        }
    }
}
