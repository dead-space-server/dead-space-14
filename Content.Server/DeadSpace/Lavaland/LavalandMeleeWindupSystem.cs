// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Interaction.Events;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// A boss strikes in melee only at someone who has stayed within its reach for <see cref="Delay"/>: walking up to it
/// for a blow and stepping back is not punished at once.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandMeleeWindupComponent : Component
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(1);

    /// <summary>No attempt at the target for this long: it has stepped away, the wind-up starts over.</summary>
    [DataField]
    public TimeSpan ResetGap = TimeSpan.FromSeconds(0.5);

    /// <summary>Target -> when it came within reach, when an attack at it was last tried.</summary>
    public readonly Dictionary<EntityUid, (TimeSpan Since, TimeSpan Last)> Targets = new();
}

public sealed class LavalandMeleeWindupSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly List<EntityUid> _stale = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandMeleeWindupComponent, AttackAttemptEvent>(OnAttackAttempt);
    }

    private void OnAttackAttempt(EntityUid uid, LavalandMeleeWindupComponent component, AttackAttemptEvent args)
    {
        // A swing at nobody in particular (a wide attack) or a disarm goes as it is.
        if (args.Cancelled || args.Disarm || args.Target is not { } target)
            return;

        var now = _timing.CurTime;
        Prune(component, now);

        if (!component.Targets.TryGetValue(target, out var entry))
            entry = (now, now);

        component.Targets[target] = (entry.Since, now);
        if (now - entry.Since < component.Delay)
            args.Cancel();
    }

    private void Prune(LavalandMeleeWindupComponent component, TimeSpan now)
    {
        _stale.Clear();
        foreach (var (target, (_, last)) in component.Targets)
        {
            if (now - last > component.ResetGap || TerminatingOrDeleted(target))
                _stale.Add(target);
        }

        foreach (var target in _stale)
        {
            component.Targets.Remove(target);
        }
    }
}
