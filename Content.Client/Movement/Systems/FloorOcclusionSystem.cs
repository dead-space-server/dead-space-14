using Content.Client.Graphics;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.StepTrigger.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.Movement.Systems;

public sealed partial class FloorOcclusionSystem : SharedFloorOcclusionSystem
{
    private static readonly ProtoId<ShaderPrototype> HorizontalCut = "HorizontalCut";

    [Dependency] private EntityQuery<SpriteComponent> _spriteQuery = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private StepTriggerSystem _stepTrigger = default!; //DS-14

    public override void Initialize()
    {
        base.Initialize();

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

        var query = EntityQueryEnumerator<FloorOcclusionComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var occlusion, out var sprite))
        {
            if (occlusion.Colliding.Count == 0)
                continue;

            var enabled = ShouldOcclude(occlusion);
            if (_sprite.HasPostShader((uid, sprite), ContentPostShaderIds.FloorOcclusion) == enabled)
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

        var shader = ProtoMan.Index(HorizontalCut).Instance();

        if (enabled)
        {
            _sprite.SetPostShader(sprite, new SpriteComponent.PostShaderArgs(ContentPostShaderIds.FloorOcclusion, shader)
            {
                Before = ContentPostShaderIds.BeforeOutlines,
            });
        }
        else
        {
            _sprite.RemovePostShader(sprite, ContentPostShaderIds.FloorOcclusion);
        }
    }
}
