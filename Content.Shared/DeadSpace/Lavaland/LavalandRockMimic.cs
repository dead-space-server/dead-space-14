// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.HealthExaminable;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Lavaland;

[Serializable, NetSerializable]
public enum LavalandRockMimicVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum LavalandRockMimicLayers : byte
{
    Base,
}

/// <summary>A boulder, or what it really is.</summary>
[Serializable, NetSerializable]
public enum LavalandRockMimicState : byte
{
    /// <summary>A boulder like any other.</summary>
    Rock,

    /// <summary>Gets up out of the boulder.</summary>
    Waking,

    /// <summary>Creeping after someone (nobody is looking).</summary>
    Walking,

    /// <summary>Up, angry, standing.</summary>
    Awake,

    Dead,
}

/// <summary>
/// The rock mimic passes for a boulder: no health on examine. Taken away once it is angry.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LavalandRockDisguiseComponent : Component;

public sealed class LavalandRockDisguiseSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandRockDisguiseComponent, ComponentStartup>(OnStartup);
    }

    // A boulder has no health to look at: on both sides, so the examine (predicted too) says nothing of it.
    private void OnStartup(Entity<LavalandRockDisguiseComponent> ent, ref ComponentStartup args)
    {
        RemComp<HealthExaminableComponent>(ent);
    }
}
