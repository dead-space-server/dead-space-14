namespace Content.Shared.DeadSpace.Surgery.Effects;

[ImplicitDataDefinitionForInheritors]
public abstract partial class SurgeryEffect
{
    [DataField]
    public float Probability = 1f;

    public abstract void RaiseEvent(SurgeryContext context, ISurgeryEffectRaiser raiser, float scale, EntityUid? user);
}

public abstract partial class SurgeryEffectBase<T> : SurgeryEffect where T : SurgeryEffectBase<T>
{
    public override void RaiseEvent(SurgeryContext context, ISurgeryEffectRaiser raiser, float scale, EntityUid? user)
    {
        if (this is T type)
            raiser.RaiseEffectEvent(type, context, scale, user);
    }
}

[ByRefEvent]
public record struct SurgeryEffectEvent<T>(T Effect, SurgeryContext Context, float Scale, EntityUid? User)
    where T : SurgeryEffectBase<T>
{
    public readonly T Effect = Effect;
    public readonly SurgeryContext Context = Context;
    public readonly float Scale = Scale;
    public readonly EntityUid? User = User;
}

public interface ISurgeryEffectRaiser
{
    void RaiseEffectEvent<T>(T effect, SurgeryContext context, float scale, EntityUid? user) where T : SurgeryEffectBase<T>;
}

public abstract partial class SurgeryEffectSystem<TEff> : EntitySystem where TEff : SurgeryEffectBase<TEff>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryEffectEvent<TEff>>(OnEffect);
    }

    protected abstract void OnEffect(ref SurgeryEffectEvent<TEff> args);
}
