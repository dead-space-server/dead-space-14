// Dead Space 14, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.CameraArchives;
using Content.Shared.Stealth.Components;
using Content.Shared.SurveillanceCamera.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.DeadSpace.CameraArchives;

public sealed class CameraConcealSystem : EntitySystem
{
    private static readonly ProtoId<ShaderPrototype> Shader = "Stealth";
    private static readonly ProtoId<ShaderPrototype> HideShader = "CameraConceal";

    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private ShaderInstance _shader = default!;
    private ShaderInstance _hide = default!;
    private readonly HashSet<IEye> _cameraEyes = new();
    private readonly Dictionary<EntityUid, (ShaderInstance? Shader, bool Screen, bool Raise)> _saved = new();

    public override void Initialize()
    {
        _shader = _prototypes.Index(Shader).InstanceUnique();
        _hide = _prototypes.Index(HideShader).InstanceUnique();
        SubscribeLocalEvent<CameraConcealedComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CameraConcealedComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CameraConcealedComponent, BeforePostShaderRenderEvent>(OnShader);
    }

    public override void FrameUpdate(float frameTime)
    {
        _cameraEyes.Clear();
        var query = EntityQueryEnumerator<SurveillanceCameraComponent, EyeComponent>();
        while (query.MoveNext(out _, out _, out var eye))
            _cameraEyes.Add(eye.Eye);

        foreach (var uid in _saved.Keys)
        {
            if (HasComp<StealthComponent>(uid) || !TryComp<SpriteComponent>(uid, out var sprite))
                continue;

            if (sprite.PostShader != null && sprite.PostShader != _shader && sprite.PostShader != _hide)
                continue;

            sprite.PostShader = _shader;
            sprite.GetScreenTexture = true;
            sprite.RaiseShaderEvent = true;
        }
    }

    private void OnStartup(Entity<CameraConcealedComponent> ent, ref ComponentStartup args)
    {
        if (HasComp<StealthComponent>(ent) || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _saved[ent] = (sprite.PostShader, sprite.GetScreenTexture, sprite.RaiseShaderEvent);
        sprite.PostShader = _shader;
        sprite.GetScreenTexture = true;
        sprite.RaiseShaderEvent = true;
    }

    private void OnShutdown(Entity<CameraConcealedComponent> ent, ref ComponentShutdown args)
    {
        if (!_saved.Remove(ent, out var saved) || Terminating(ent) || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        sprite.PostShader = saved.Shader;
        sprite.GetScreenTexture = saved.Screen;
        sprite.RaiseShaderEvent = saved.Raise;
    }

    private void OnShader(Entity<CameraConcealedComponent> ent, ref BeforePostShaderRenderEvent args)
    {
        if (HasComp<StealthComponent>(ent))
            return;

        var parent = Transform(ent).ParentUid;
        var reference = parent.IsValid()
            ? _transform.GetWorldPosition(parent)
            : _transform.GetWorldPosition(ent);
        if (IsCameraEye(args.Viewport.Eye))
        {
            args.Sprite.PostShader = _hide;
            return;
        }

        args.Sprite.PostShader = _shader;
        args.Sprite.GetScreenTexture = true;
        args.Sprite.RaiseShaderEvent = true;
        var screen = args.Viewport.WorldToLocal(reference);
        _shader.SetParameter("reference", new Vector2(-screen.X, screen.Y));
        _shader.SetParameter("visibility", 1f);
    }

    private bool IsCameraEye(IEye? eye)
    {
        if (eye == null)
            return false;

        if (_cameraEyes.Contains(eye))
            return true;

        foreach (var known in _cameraEyes)
        {
            if (known.Position.MapId != eye.Position.MapId)
                continue;

            if ((known.Position.Position - eye.Position.Position).LengthSquared() > 0.04f)
                continue;

            if (!known.Rotation.EqualsApprox(eye.Rotation, 0.01))
                continue;

            return true;
        }

        return false;
    }
}
