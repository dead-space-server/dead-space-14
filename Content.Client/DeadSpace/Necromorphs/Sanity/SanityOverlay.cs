using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
namespace Content.Client.DeadSpace.Sanity;

public sealed partial class SanityOverlay : Overlay
{
    private readonly float _outerCircleValue = 0.9f;
    private readonly float _innerCircleValue = 0.65f;
    private readonly float _outerCircleMaxRadius = 0.08f;
    private readonly float _innerCircleMaxRadius = 0.04f;
    public float Value = 0f;
    private readonly float _darknessAlphaOuter = 0.94f;
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;
    private readonly ShaderInstance _circleMaskShader = default!;
    private const string ShaderName = "DeadSpaceGradientCircleMask";

    [Dependency] private IPrototypeManager _prototypeManager = default!;

    public SanityOverlay()
    {
        IoCManager.InjectDependencies(this);
        _circleMaskShader = _prototypeManager.Index<ShaderPrototype>(ShaderName).InstanceUnique();
    }
    protected override void Draw(in OverlayDrawArgs args)
    {
        _circleMaskShader.SetParameter("color", Vector3.Zero);
        _circleMaskShader.SetParameter("outerCircleRadius", _outerCircleValue);
        _circleMaskShader.SetParameter("innerCircleRadius", _innerCircleValue);
        _circleMaskShader.SetParameter("outerCircleMaxRadius", _outerCircleMaxRadius);
        _circleMaskShader.SetParameter("innerCircleMaxRadius", _innerCircleMaxRadius);
        _circleMaskShader.SetParameter("time", Value);
        _circleMaskShader.SetParameter("darknessAlphaOuter", _darknessAlphaOuter);

        var screenHandle = args.ScreenHandle;
        var viewport = args.ViewportBounds;

        screenHandle.UseShader(_circleMaskShader);
        screenHandle.DrawRect(viewport, Color.White);
        screenHandle.UseShader(null);
    }
}
