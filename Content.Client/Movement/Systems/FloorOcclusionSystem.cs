using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.StepTrigger.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.Movement.Systems;

public sealed class FloorOcclusionSystem : SharedFloorOcclusionSystem
{
    private static readonly ProtoId<ShaderPrototype> HorizontalCut = "HorizontalCut";

    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly StepTriggerSystem _stepTrigger = default!; //DS-14

    private EntityQuery<SpriteComponent> _spriteQuery;

    public override void Initialize()
    {
        base.Initialize();

        _spriteQuery = GetEntityQuery<SpriteComponent>();

        SubscribeLocalEvent<FloorOcclusionComponent, ComponentStartup>(OnOcclusionStartup);
        SubscribeLocalEvent<FloorOcclusionComponent, ComponentShutdown>(OnOcclusionShutdown);
        SubscribeLocalEvent<FloorOcclusionComponent, AfterAutoHandleStateEvent>(OnOcclusionAuto);
    }

    private void OnOcclusionAuto(Entity<FloorOcclusionComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        SetShader(ent.Owner, ShouldOcclude(ent.Comp)); //DS-14
    }

    private void OnOcclusionStartup(Entity<FloorOcclusionComponent> ent, ref ComponentStartup args)
    {
        SetShader(ent.Owner, ShouldOcclude(ent.Comp)); //DS-14
    }

    private void OnOcclusionShutdown(Entity<FloorOcclusionComponent> ent, ref ComponentShutdown args)
    {
        SetShader(ent.Owner, false);
    }

    protected override void SetEnabled(Entity<FloorOcclusionComponent> entity)
    {
        SetShader(entity.Owner, ShouldOcclude(entity.Comp)); //DS-14
    }

    //DS-14 start
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var shader = _proto.Index(HorizontalCut).Instance();
        var query = EntityQueryEnumerator<FloorOcclusionComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var occlusion, out var sprite))
        {
            if (occlusion.Colliding.Count == 0)
                continue;

            var enabled = ShouldOcclude(occlusion);
            if ((sprite.PostShader == shader) == enabled)
                continue;

            SetShader(uid, enabled);
        }
    }

    private bool ShouldOcclude(FloorOcclusionComponent occlusion)
    {
        foreach (var occluder in occlusion.Colliding)
        {
            if (!Exists(occluder) || _stepTrigger.IsBlacklistBlocked(occluder))
                continue;

            return true;
        }

        return false;
    }
    //DS-14 end

    private void SetShader(Entity<SpriteComponent?> sprite, bool enabled)
    {
        if (!_spriteQuery.Resolve(sprite.Owner, ref sprite.Comp, false))
            return;

        var shader = _proto.Index(HorizontalCut).Instance();

        if (sprite.Comp.PostShader is not null && sprite.Comp.PostShader != shader)
            return;

        if (enabled)
        {
            sprite.Comp.PostShader = shader;
        }
        else
        {
            sprite.Comp.PostShader = null;
        }
    }
}
