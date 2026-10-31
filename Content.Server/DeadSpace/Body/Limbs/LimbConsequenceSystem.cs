// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.DeadSpace.Body.Limbs;

public sealed class LimbConsequenceSystem : EntitySystem
{
    private static readonly SoundSpecifier Hiss = new SoundPathSpecifier("/Audio/Items/hiss.ogg");

    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LimbBodyComponent, LimbHookEvent>(OnHook);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<LimbWhistleComponent, LimbBodyComponent>();
        while (query.MoveNext(out var uid, out var whistle, out var body))
        {
            if (_mobs.IsDead(uid) || !body.LungWhistle)
                continue;

            whistle.Accumulator += frameTime;
            if (whistle.Accumulator < LimbWhistleComponent.Period)
                continue;

            whistle.Accumulator -= LimbWhistleComponent.Period;
            _audio.PlayPvs(Hiss, uid);
        }
    }

    private void OnHook(Entity<LimbBodyComponent> ent, ref LimbHookEvent args)
    {
        if (args.Hook != LimbHooks.Whistle || ent.Comp.LungWhistle)
            return;

        ent.Comp.LungWhistle = true;
        EnsureComp<LimbWhistleComponent>(ent);
    }
}
