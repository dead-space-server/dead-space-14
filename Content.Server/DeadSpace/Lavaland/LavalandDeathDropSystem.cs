// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Mobs;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>Loot that may fall out of a creature once it dies, each entry with its own chance.</summary>
[RegisterComponent]
public sealed partial class LavalandDeathDropComponent : Component
{
    [DataField(required: true)]
    public List<LavalandDeathDropEntry> Drops = new();

    [ViewVariables]
    public bool Dropped;
}

[DataDefinition]
public sealed partial class LavalandDeathDropEntry
{
    [DataField(required: true)]
    public EntProtoId Id;

    /// <summary>0-1.</summary>
    [DataField]
    public float Chance = 1f;
}

public sealed class LavalandDeathDropSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandDeathDropComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(EntityUid uid, LavalandDeathDropComponent component, MobStateChangedEvent args)
    {
        // Once: revived and killed again, it does not drop a second time.
        if (args.NewMobState != MobState.Dead || component.Dropped)
            return;

        component.Dropped = true;
        var coordinates = _transform.GetMapCoordinates(uid);
        foreach (var drop in component.Drops)
        {
            if (_random.Prob(Math.Clamp(drop.Chance, 0f, 1f)))
                Spawn(drop.Id, coordinates);
        }
    }
}
