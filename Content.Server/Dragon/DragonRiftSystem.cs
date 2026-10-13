using Content.Server.Chat.Systems;
using Content.Server.NPC;
using Content.Server.NPC.Systems;
using Content.Server.Pinpointer;
using Content.Shared.Dragon;
using Content.Shared.Examine;
using Content.Shared.Sprite;
//DS14-start
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Robust.Shared.Random;
//DS14-end
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Serialization.Manager;
using System.Numerics;
using Content.Shared.Damage.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Server.Dragon;

/// <summary>
/// Handles events for rift entities and rift updating.
/// </summary>
public sealed class DragonRiftSystem : EntitySystem
{
    [Dependency] private readonly Content.Server.DeadSpace.CentComm.GameRuleStationSystem _ruleStation = default!; // DS14
    [Dependency] private readonly DragonSystem _dragon = default!;
    [Dependency] private readonly ISerializationManager _serManager = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IRobustRandom _random = default!; //DS14

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DragonRiftComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<DragonRiftComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DragonRiftComponent, AnchorStateChangedEvent>(OnAnchorChange);
        SubscribeLocalEvent<DragonRiftComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnGetState(Entity<DragonRiftComponent> ent, ref ComponentGetState args)
    {
        args.State = new DragonRiftComponentState
        {
            State = ent.Comp.State,
        };
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DragonRiftComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.State != DragonRiftState.Finished && comp.Accumulator >= comp.MaxAccumulator)
            {
                // TODO: When we get autocall you can buff if the rift finishes / 3 rifts are up
                // for now they just keep 3 rifts up.

                if (comp.Dragon != null)
                    _dragon.RiftCharged(comp.Dragon.Value);

                comp.Accumulator = comp.MaxAccumulator;
                RemComp<DamageableComponent>(uid);
                comp.State = DragonRiftState.Finished;
                Dirty(uid, comp);
            }
            else if (comp.State != DragonRiftState.Finished)
            {
                comp.Accumulator += frameTime;
            }

            comp.SpawnAccumulator += frameTime;

            if (comp.State < DragonRiftState.AlmostFinished && comp.Accumulator > comp.MaxAccumulator / 2f)
            {
                comp.State = DragonRiftState.AlmostFinished;
                Dirty(uid, comp);

                var msg = Loc.GetString("carp-rift-warning",
                    ("location", FormattedMessage.RemoveMarkupOrThrow(_navMap.GetNearestBeaconString((uid, xform)))));
                _ruleStation.Announce(uid, msg, playSound: false, colorOverride: Color.Red); // DS14
                _audio.PlayGlobal("/Audio/Misc/notice1.ogg", _ruleStation.GetEventPlayers(uid), true); // DS14
                _navMap.SetBeaconEnabled(uid, true);
            }

            if (comp.SpawnAccumulator > comp.SpawnCooldown)
            {
                comp.SpawnAccumulator -= comp.SpawnCooldown;
//DS14-start
                if (_random.NextFloat() > comp.Chance)
                    continue;

                string? prototype = null;

                if (comp.RarePrototypes.Count > 0 && _random.NextFloat() < comp.RareChance)
                {
                    prototype = _random.Pick(comp.RarePrototypes);
                }
                else if (comp.Prototypes.Count > 0)
                {
                    prototype = _random.Pick(comp.Prototypes);
                }

                if (prototype == null)
                    continue;

                var ent = Spawn(prototype, xform.Coordinates);
//DS14-end
                // Update their look to match the leader.
                if (TryComp<RandomSpriteComponent>(comp.Dragon, out var randomSprite))
                {
                    var spawnedSprite = EnsureComp<RandomSpriteComponent>(ent);
                    _serManager.CopyTo(randomSprite, ref spawnedSprite, notNullableOverride: true);
                    Dirty(ent, spawnedSprite);
                }

                if (comp.Dragon != null)
                    _npc.SetBlackboard(ent, NPCBlackboard.FollowTarget, new EntityCoordinates(comp.Dragon.Value, Vector2.Zero));
            }
        }
    }

    private void OnExamined(EntityUid uid, DragonRiftComponent component, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("carp-rift-examine", ("percentage", MathF.Round(component.Accumulator / component.MaxAccumulator * 100))));
    }

    private void OnAnchorChange(EntityUid uid, DragonRiftComponent component, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored && component.State == DragonRiftState.Charging)
        {
            QueueDel(uid);
        }
    }

    private void OnShutdown(EntityUid uid, DragonRiftComponent comp, ComponentShutdown args)
    {
        if (!TryComp<DragonComponent>(comp.Dragon, out var dragon) || dragon.Weakened)
            return;

        _dragon.RiftDestroyed(comp.Dragon.Value, dragon);
    }
}
