using Content.Shared.Ninja.Components; //DS14
using Content.Shared.Ninja.Systems;
using Content.Shared.Examine; //DS14
using Content.Shared.Mobs.Components; //DS14
using System.Numerics; //DS14
using Robust.Client.GameObjects; //DS14

namespace Content.Client.Ninja.Systems;

public sealed class NinjaSuitSystem : SharedNinjaSuitSystem
{
    //DS14-start
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<PlayNinjaRevealEffectMessage>(OnRevealEffect);
    }

    private void OnRevealEffect(PlayNinjaRevealEffectMessage msg)
    {
        _mobs.Clear();
        var source = GetEntity(msg.Source);

        if (!TryComp<NinjaSuitComponent>(source, out var suitComp))
            return;

        var sourcePos = _transform.GetMapCoordinates(source);

        _entityLookup.GetEntitiesInRange(sourcePos, suitComp.RevealEffectDistance, _mobs);

        foreach (var (mob, _) in _mobs)
        {
            if (mob == source)
                continue;

            var mobPos = _transform.GetMapCoordinates(mob);

            if (!_examine.InRangeUnOccluded(sourcePos, mobPos, suitComp.RevealEffectDistance, null))
                continue;

            var ent = Spawn(suitComp.RevealEffect, mobPos);

            if (TryComp<SpriteComponent>(ent, out var sprite))
                _sprite.SetOffset((ent, sprite), new Vector2(0, 1));

            _transform.SetParent(ent, Transform(ent), mob);
        }
    }
    //DS14-end
}
