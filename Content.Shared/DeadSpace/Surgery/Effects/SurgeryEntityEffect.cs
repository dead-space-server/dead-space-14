using Content.Shared.EntityEffects;

namespace Content.Shared.DeadSpace.Surgery.Effects;

public sealed partial class SurgeryEntityEffect : SurgeryEffectBase<SurgeryEntityEffect>
{
    [DataField]
    public SurgeryEntityTarget Target = SurgeryEntityTarget.Patient;

    [DataField(required: true)]
    public EntityEffect[] Effects = [];

    [DataField]
    public float Scale = 1f;
}

public sealed partial class SurgeryEntityEffectSystem : SurgeryEffectSystem<SurgeryEntityEffect>
{
    [Dependency] private readonly SharedEntityEffectsSystem _effects = default!;

    protected override void OnEffect(ref SurgeryEffectEvent<SurgeryEntityEffect> args)
    {
        if (args.Context.GetEntity(args.Effect.Target) is not { } ent)
            return;

        _effects.ApplyEffects(ent, args.Effect.Effects, args.Effect.Scale * args.Scale, args.User);
    }
}
