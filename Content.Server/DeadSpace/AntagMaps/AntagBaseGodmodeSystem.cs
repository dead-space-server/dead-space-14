using Content.Server.Antag;
using Content.Server.Damage.Systems;
using Content.Shared.Ghost;
using Robust.Shared.Player;

namespace Content.Server.DeadSpace.AntagMaps;

public sealed class AntagBaseGodmodeSystem : EntitySystem
{
    [Dependency] private readonly GodmodeSystem _god = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AfterAntagEntitySelectedEvent>(OnAntag);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
    }

    private void OnAntag(ref AfterAntagEntitySelectedEvent ev)
    {
        Grant(ev.EntityUid, responseOnly: false);
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        Grant(ev.Entity, responseOnly: true);
    }

    private void Grant(EntityUid uid, bool responseOnly)
    {
        if (HasComp<GhostComponent>(uid))
            return;

        if (!TryComp(uid, out TransformComponent? xform) || xform.MapUid is not { } map)
            return;

        if (!TryComp<PreparedRoundMapComponent>(map, out var prepared) || !prepared.SleepWhenEmpty)
            return;

        if (responseOnly && prepared.ShuttleId == null)
            return;

        var comp = EnsureComp<AntagBaseGodmodeComponent>(uid);
        comp.HomeMap = map;
        _god.EnableGodmode(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<AntagBaseGodmodeComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.MapUid == comp.HomeMap)
                continue;

            Clear(uid);
        }
    }

    private void Clear(EntityUid uid)
    {
        _god.DisableGodmode(uid);
        RemComp<AntagBaseGodmodeComponent>(uid);
    }
}
