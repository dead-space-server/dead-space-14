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
            if (!part.Visible)
                continue;

            Texture? texture = part.Texture;
            if (texture == null && part.ActualRsi != null && part.ActualRsi.TryGetState(part.RsiState, out var state))
            {
                var frame = state.DelayCount == 0 ? 0 : Math.Min(part.AnimationFrame, state.DelayCount - 1);
                texture = state.GetFrame(RsiDirection.South, frame);
            }

            if (texture == null)
                continue;

            var tintZone = zone == LimbZone.Eyes ? LimbZone.Head : zone;
            handle.DrawTextureRect(texture, box, Color.White);
            handle.DrawTextureRect(texture, box, Tint(tintZone));
            if (zone != LimbZone.Torso || texture.Height <= 0)
                continue;

            var top = 18f / 32f;
            var span = 4f / 32f;
            var sub = new UIBox2(0f, texture.Height * top, texture.Width, texture.Height * (top + span));
            var destTop = box.Top + box.Height * top;
            var dest = new UIBox2(box.Left, destTop, box.Right, destTop + box.Height * span);
            handle.DrawTextureRectRegion(texture, dest, sub, Color.White);
            handle.DrawTextureRectRegion(texture, dest, sub, Tint(LimbZone.Groin));
        }
    }

    private Color Tint(LimbZone zone)
    {
        var index = (int) zone;
        var shade = (uint) index < (uint) Shades.Length ? Shades[index] : LimbScan.Fine;
        if (zone is LimbZone.Eyes or LimbZone.Mouth && (uint) LimbZone.Head < (uint) Shades.Length && Shades[(int) LimbZone.Head] == LimbScan.Missing)
            shade = LimbScan.Missing;

        var color = shade switch
        {
            LimbScan.Missing => Color.Black,
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
