// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Input;

namespace Content.Client.DeadSpace.Body.Limbs;

public sealed class AnalyzerLimbView : Control
{
    public static readonly (LimbZone Zone, HumanoidVisualLayers Layer)[] Layers =
    {
        (LimbZone.Head, HumanoidVisualLayers.Head),
        (LimbZone.Torso, HumanoidVisualLayers.Chest),
        (LimbZone.ArmRight, HumanoidVisualLayers.RArm),
        (LimbZone.ArmLeft, HumanoidVisualLayers.LArm),
        (LimbZone.HandRight, HumanoidVisualLayers.RHand),
        (LimbZone.HandLeft, HumanoidVisualLayers.LHand),
        (LimbZone.LegRight, HumanoidVisualLayers.RLeg),
        (LimbZone.LegLeft, HumanoidVisualLayers.LLeg),
        (LimbZone.FootRight, HumanoidVisualLayers.RFoot),
        (LimbZone.FootLeft, HumanoidVisualLayers.LFoot),
        (LimbZone.Eyes, HumanoidVisualLayers.Eyes),
    };

    public byte[] Shades = Array.Empty<byte>();
    public EntityUid? Entity;
    public LimbZone? Selected;
    public event Action<LimbZone?>? OnZone;

    private readonly IEntityManager _entities;
    private readonly SpriteSystem _sprites;

    public AnalyzerLimbView()
    {
        MouseFilter = MouseFilterMode.Stop;
        _entities = IoCManager.Resolve<IEntityManager>();
        _sprites = _entities.System<SpriteSystem>();
        OnKeyBindDown += OnClick;
    }

    private void OnClick(GUIBoundKeyEventArgs args)
    {
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        var pixel = args.RelativePosition / Size * 32f;
        var zone = LimbDollLayout.SpriteHit(pixel);
        if (zone is LimbZone.Eyes or LimbZone.Mouth)
            zone = LimbZone.Head;
        Selected = zone;
        OnZone?.Invoke(Selected);
        args.Handle();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (Entity is not { } uid || !_entities.TryGetComponent(uid, out SpriteComponent? sprite))
            return;

        var box = new UIBox2(Vector2.Zero, PixelSize);
        foreach (var (zone, layer) in Layers)
        {
            if (!_sprites.LayerMapTryGet((uid, sprite), layer, out var index, false))
                continue;

            var part = sprite[index];
            var tintZone = zone == LimbZone.Eyes ? LimbZone.Head : zone;
            if (!part.Visible && Missing(tintZone))
                continue;

            Texture? texture = part.Texture;
            if (texture == null && part.ActualRsi != null && part.ActualRsi.TryGetState(part.RsiState, out var state))
            {
                var frame = state.DelayCount == 0 ? 0 : Math.Min(part.AnimationFrame, state.DelayCount - 1);
                texture = state.GetFrame(RsiDirection.South, frame);
            }

            if (texture == null)
                continue;

            if (zone != LimbZone.Torso || texture.Height <= 0)
            {
                handle.DrawTextureRect(texture, box, Color.White);
                handle.DrawTextureRect(texture, box, Tint(tintZone));
                continue;
            }

            var cut = 18f / 32f;
            var chestSrc = new UIBox2(0f, 0f, texture.Width, texture.Height * cut);
            var groinSrc = new UIBox2(0f, texture.Height * cut, texture.Width, texture.Height);
            var chestDst = new UIBox2(box.Left, box.Top, box.Right, box.Top + box.Height * cut);
            var groinDst = new UIBox2(box.Left, chestDst.Bottom, box.Right, box.Bottom);
            handle.DrawTextureRectRegion(texture, chestDst, chestSrc, Color.White);
            handle.DrawTextureRectRegion(texture, chestDst, chestSrc, Tint(LimbZone.Torso));
            handle.DrawTextureRectRegion(texture, groinDst, groinSrc, Color.White);
            handle.DrawTextureRectRegion(texture, groinDst, groinSrc, Tint(LimbZone.Groin));
        }
    }

    private bool Missing(LimbZone zone)
    {
        var index = (int) zone;
        return (uint) index < (uint) Shades.Length && Shades[index] == LimbScan.Missing;
    }

    private Color Tint(LimbZone zone)
    {
        var index = (int) zone;
        var shade = (uint) index < (uint) Shades.Length ? Shades[index] : LimbScan.Fine;
        if (zone is LimbZone.Eyes or LimbZone.Mouth && (uint) LimbZone.Head < (uint) Shades.Length && Shades[(int) LimbZone.Head] == LimbScan.Missing)
            shade = LimbScan.Missing;

        var color = shade switch
        {
            LimbScan.Missing => Color.FromHex("#6a6a72"),
            LimbScan.Hurt => Color.FromHex("#d6c14a"),
            LimbScan.Bad => Color.FromHex("#e07a2f"),
            LimbScan.Critical => Color.FromHex("#c43838"),
            _ => Color.FromHex("#3d8ec4"),
        };

        var alpha = shade == LimbScan.Fine ? 0.75f : 0.82f;
        if (Selected == zone)
            alpha = 0.92f;

        return color.WithAlpha(alpha);
    }
}
