using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Animals;

public sealed partial class PackCallSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobs = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly List<EntityUid> _joiners = new();
    private readonly List<PendingCall> _pending = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PackCallComponent, EmoteEvent>(OnEmote);
    }

    private void OnEmote(Entity<PackCallComponent> ent, ref EmoteEvent args)
    {
        if (args.Emote.ID != ent.Comp.Emote)
            return;

        var now = _timing.CurTime;
        if (now < ent.Comp.NextCall)
            return;

        ent.Comp.NextCall = now + ent.Comp.Cooldown;
        _joiners.Clear();

        foreach (var other in _lookup.GetEntitiesInRange<PackCallComponent>(_transform.GetMapCoordinates(ent.Owner), ent.Comp.Range))
        {
            if (other.Owner == ent.Owner)
                continue;

            if (other.Comp.Emote != ent.Comp.Emote)
                continue;

            if (now < other.Comp.NextCall || !_mobs.IsAlive(other.Owner))
                continue;

            other.Comp.NextCall = now + other.Comp.Cooldown;
            _joiners.Add(other.Owner);
        }

        foreach (var joiner in _joiners)
        {
            var delay = ent.Comp.MaxJoinDelay <= TimeSpan.Zero
                ? TimeSpan.Zero
                : _random.Next(ent.Comp.MinJoinDelay, ent.Comp.MaxJoinDelay);

            if (delay <= TimeSpan.Zero)
            {
                _chat.TryEmoteWithChat(joiner, ent.Comp.Emote);
                continue;
            }

            _pending.Add(new PendingCall(joiner, now + delay, ent.Comp.Emote));
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        for (var i = _pending.Count - 1; i >= 0; i--)
        {
            var pending = _pending[i];
            if (pending.When > now)
                continue;

            _pending.RemoveAt(i);
            if (!Exists(pending.Uid) || !_mobs.IsAlive(pending.Uid))
                continue;

            _chat.TryEmoteWithChat(pending.Uid, pending.Emote);
        }
    }

    private readonly record struct PendingCall(EntityUid Uid, TimeSpan When, ProtoId<EmotePrototype> Emote);
}
