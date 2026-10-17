// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Lavaland;

/// <summary>Hooked by the mad miner: no walking away until the hook is gone (it ends, or is pulled out).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class LavalandMadMinerHookedComponent : Component
{
    [ViewVariables, AutoNetworkedField] public EntityUid Miner;
    [ViewVariables] public bool Removing;
}

[RegisterComponent]
public sealed partial class LavalandMadMinerHookVisualComponent : Component
{
    [ViewVariables] public EntityUid Target;
}

[Serializable, NetSerializable]
public sealed partial class LavalandRemoveMadMinerHookDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Keeps a hooked player in place; a hook left behind by a dead or vanished miner falls off.</summary>
public sealed class LavalandMadMinerHookSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandMadMinerHookedComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
        SubscribeLocalEvent<LavalandMadMinerHookedComponent, ComponentStartup>(OnChanged);
        SubscribeLocalEvent<LavalandMadMinerHookedComponent, ComponentShutdown>(OnChanged);
        SubscribeLocalEvent<LavalandMadMinerHookedComponent, AfterAutoHandleStateEvent>(OnChanged);
    }

    /// <summary>
    /// Hooks the player to this miner. The miner is set after the component starts, so whether the player can move is
    /// worked out again here - at startup nobody held the hook yet and the player was left free to walk.
    /// </summary>
    public void SetMiner(Entity<LavalandMadMinerHookedComponent> ent, EntityUid miner)
    {
        ent.Comp.Miner = miner;
        ent.Comp.Removing = false;
        Dirty(ent);
        _blocker.UpdateCanMove(ent);
    }

    private void OnUpdateCanMove(Entity<LavalandMadMinerHookedComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (ent.Comp.LifeStage <= ComponentLifeStage.Running && MinerHolds(ent.Comp))
            args.Cancel();
    }

    private void OnChanged<TEvent>(Entity<LavalandMadMinerHookedComponent> ent, ref TEvent args)
    {
        _blocker.UpdateCanMove(ent);
    }

    private bool MinerHolds(LavalandMadMinerHookedComponent hooked)
    {
        return hooked.Miner.IsValid() && !TerminatingOrDeleted(hooked.Miner) && !_mobState.IsDead(hooked.Miner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_net.IsClient)
            return;

        // Its miner is gone: the hook comes loose instead of holding the player forever.
        var query = EntityQueryEnumerator<LavalandMadMinerHookedComponent>();
        while (query.MoveNext(out var uid, out var hooked))
        {
            if (!MinerHolds(hooked))
                RemCompDeferred<LavalandMadMinerHookedComponent>(uid);
        }
    }
}
