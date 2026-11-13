using Content.Server.Destructible;
using Content.Server.Gatherable.Components;
using Content.Shared.EntityTable;
using Content.Shared.Interaction;
using Content.Shared.Tag;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Whitelist;
using Content.Shared.Wieldable.Components; // DS14
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.Gatherable;

public sealed partial class GatherableSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private DestructibleSystem _destructible = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private TagSystem _tagSystem = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private EntityTableSystem _entityTable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GatherableComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<GatherableComponent, AttackedEvent>(OnAttacked);
        InitializeProjectile();
    }

    private void OnAttacked(Entity<GatherableComponent> gatherable, ref AttackedEvent args)
    {
        if (_whitelistSystem.IsWhitelistFailOrNull(gatherable.Comp.ToolWhitelist, args.Used))
            return;

        // DS14-Start
        if (TryComp<WieldableComponent>(args.Used, out var wieldable) && !wieldable.Wielded)
            return;
        // DS14-End

        var ev = new GatherableGatherAttemptEvent(args.Used, args.User, args.ClickLocation);
        RaiseLocalEvent(gatherable.Owner, ev);
        if (ev.Cancelled)
            return;

        Gather(gatherable, args.User);
    }

    private void OnActivate(Entity<GatherableComponent> gatherable, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        if (_whitelistSystem.IsWhitelistFailOrNull(gatherable.Comp.ToolWhitelist, args.User))
            return;

        var ev = new GatherableGatherAttemptEvent(args.User, args.User, Transform(gatherable.Owner).Coordinates);
        RaiseLocalEvent(gatherable.Owner, ev);
        if (ev.Cancelled)
            return;

        Gather(gatherable, args.User);
        args.Handled = true;
    }

    public void Gather(EntityUid gatheredUid, EntityUid? gatherer = null, GatherableComponent? component = null)
    {
        if (!Resolve(gatheredUid, ref component))
            return;

        if (TryComp<SoundOnGatherComponent>(gatheredUid, out var soundComp))
        {
            _audio.PlayPvs(soundComp.Sound, Transform(gatheredUid).Coordinates);
        }

        // Complete the gathering process
        _destructible.DestroyEntity(gatheredUid);

        // Spawn the loot!
        if (component.Loot == null)
            return;

        var pos = _transform.GetMapCoordinates(gatheredUid);

        foreach (var (tag, table) in component.Loot)
        {
            if (tag != "All")
            {
                if (gatherer != null && !_tagSystem.HasTag(gatherer.Value, tag))
                    continue;
            }
            var spawnLoot = _entityTable.GetSpawns(table);
            foreach (var loot in spawnLoot)
            {
                var spawnPos = pos.Offset(_random.NextVector2(component.GatherOffset));
                Spawn(loot, spawnPos);
            }
        }
    }
}

public sealed partial class GatherableGatherAttemptEvent(
    EntityUid used,
    EntityUid user,
    EntityCoordinates clickLocation) : CancellableEntityEventArgs
{
    public EntityUid Used { get; } = used;
    public EntityUid User { get; } = user;
    public EntityCoordinates ClickLocation { get; } = clickLocation;
}
