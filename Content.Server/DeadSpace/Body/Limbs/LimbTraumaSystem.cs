// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Psychiatry;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Psychiatry;
using Content.Shared.Stunnable;

namespace Content.Server.DeadSpace.Body.Limbs;

public sealed class LimbTraumaSystem : EntitySystem
{
    [Dependency] private readonly PsychiatrySystem _psychiatry = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LimbBodyComponent, LimbConcussionEvent>(OnConcussion);
        SubscribeLocalEvent<LimbBodyComponent, LimbBrainRuptureEvent>(OnBrain);
    }

    private void OnConcussion(Entity<LimbBodyComponent> ent, ref LimbConcussionEvent args)
    {
        var time = TimeSpan.FromSeconds(args.Heavy ? 8 : 4);
        _stun.TryAddParalyzeDuration(ent, time);
        _psychiatry.TryOnsetOrEscalate(ent, SchizophreniaStage.Latent, "blunt-head", harm: true);
    }

    private void OnBrain(Entity<LimbBodyComponent> ent, ref LimbBrainRuptureEvent args)
    {
        _psychiatry.ApplyNew(ent.Owner, SchizophreniaStage.Acute, pillForced: true, reason: "brain-rupture");
    }
}
