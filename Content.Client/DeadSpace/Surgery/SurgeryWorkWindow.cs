// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Surgery;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Utility;
using Robust.Shared.Input;
using Robust.Shared.Maths;

namespace Content.Client.DeadSpace.Surgery;

public sealed class SurgeryChestMap : Control
{
    public event Action<byte>? Picked;
    public event Action? Severed;

    public bool Sever;
    public bool Insert;

    private readonly record struct Spot(LimbOrgan Organ, string State, int X, int Y, int W, int H, float Cx, float Cy, float Height);

    private readonly List<Spot> _spots = new();
    private byte[] _present = Array.Empty<byte>();
    private byte[] _missing = Array.Empty<byte>();
    private byte _chosen = 255;
    private LimbOrgan? _hover;
    private RSI? _organs;

    public SurgeryChestMap()
    {
        MouseFilter = MouseFilterMode.Stop;
        MinSize = new Vector2(150, 160);
    }

    public void Show(byte[] organs, byte[] missing, byte chosen, bool sever, bool insert, LimbZone zone)
    {
        _present = organs;
        _missing = missing;
        _chosen = chosen;
        Sever = sever;
        Insert = insert;
        _hover = null;
        _spots.Clear();
        var zoneName = zone.ToString();
        foreach (var organ in IoCManager.Resolve<IPrototypeManager>().EnumeratePrototypes<SurgeryOrganPrototype>())
        {
            if (organ.Zone != zoneName || !Enum.TryParse<LimbOrgan>(organ.ID, out var kind))
                continue;

            if (!Has(kind) && !Missing(kind))
                continue;

            foreach (var spot in organ.Spots)
                _spots.Add(new Spot(kind, spot.State, spot.X, spot.Y, spot.W, spot.H, spot.Cx, spot.Cy, spot.Height));
        }
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        var organ = Hit(args.RelativePosition);
        if (Lodged != null && _lodgedBox.Width > 1f && _lodgedBox.Contains(args.RelativePosition))
        {
            TakeLodged?.Invoke();
            args.Handle();
            return;
        }

        if (organ == null)
            return;

        if (Insert && !Missing(organ.Value))
            return;

        if (!Insert && !Has(organ.Value))
            return;

        Picked?.Invoke((byte) organ.Value);
        args.Handle();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        _hover = Hit(args.RelativePosition);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var size = PixelSize;
        var cavity = UIBox2.FromDimensions(new Vector2(8, 8), size - new Vector2(16, 16));
        handle.DrawRect(cavity, new Color(0.45f, 0.14f, 0.14f, 1f));

        foreach (var spot in _spots)
        {
            if (!Sheet().TryGetState(spot.State, out var state))
                continue;

            var missing = Missing(spot.Organ);
            var lit = missing && _chosen == (byte) spot.Organ;
            var rect = OrganRect(spot, size);
            if (_hover == spot.Organ || Sever && _chosen == (byte) spot.Organ || lit)
                handle.DrawRect(rect, new Color(1f, 0.86f, 0.25f, lit ? 0.28f : 0.45f));

            var scale = rect.Height / spot.H;
            var origin = new Vector2(rect.Left - spot.X * scale, rect.Top - spot.Y * scale);
            var ink = missing
                ? lit ? new Color(0.78f, 0.8f, 0.84f, 1f) : new Color(0.28f, 0.28f, 0.3f, 1f)
                : Color.White;
            handle.DrawTextureRect(state.GetFrame(RsiDirection.South, 0), UIBox2.FromDimensions(origin, new Vector2(32, 32) * scale), ink);
        }

        if (Lodged != null)
        {
            _lodgedBox = UIBox2.FromDimensions(new Vector2(size.X / 2f - 16f, size.Y - 40f), new Vector2(32, 32));
            handle.DrawTextureRect(Lodged, _lodgedBox, Color.White);
        }
    }

    public event Action? TakeLodged;
    public Texture? Lodged;
    private UIBox2 _lodgedBox;

    private UIBox2 OrganRect(Spot spot, Vector2 size)
    {
        var height = size.Y * spot.Height;
        var width = height * spot.W / spot.H;
        var center = new Vector2(size.X * spot.Cx, size.Y * spot.Cy);
        return UIBox2.FromDimensions(center - new Vector2(width, height) / 2f, new Vector2(width, height));
    }

    private bool Has(LimbOrgan organ)
    {
        foreach (var item in _present)
        {
            if (item == (byte) organ)
                return true;
        }

        return false;
    }

    private bool Missing(LimbOrgan organ)
    {
        foreach (var item in _missing)
        {
            if (item == (byte) organ)
                return true;
        }

        return false;
    }

    private LimbOrgan? Hit(Vector2 point)
    {
        for (var i = _spots.Count - 1; i >= 0; i--)
        {
            var spot = _spots[i];
            if (OrganRect(spot, PixelSize).Contains(point))
                return spot.Organ;
        }

        return null;
    }

    private RSI Sheet()
    {
        return _organs ??= IoCManager.Resolve<IResourceCache>().GetResource<RSIResource>("/Textures/Mobs/Species/Human/organs.rsi").RSI;
    }
}

public sealed class SurgeryStageView : Control
{
    public const float ViewScale = 6f;

    public readonly SpriteView View;
    public bool Inside;
    public bool Down;
    public Vector2 Meters;
    public event Action<byte>? Picked;
    public event Action? Dragged;

    private bool _organs;
    private bool _held;
    private Vector2 _dragFrom;
    private byte[] _present = Array.Empty<byte>();

    public SurgeryStageView()
    {
        MinSize = new Vector2(192, 192);
        View = new SpriteView
        {
            Scale = new Vector2(ViewScale, ViewScale),
            Stretch = SpriteView.StretchMode.None,
            OverrideDirection = Direction.South,
            MouseFilter = MouseFilterMode.Ignore,
            MinSize = new Vector2(192, 192),
        };
        MouseFilter = MouseFilterMode.Stop;
        AddChild(View);
    }

    public void SetPatient(EntityUid patient)
    {
        View.SetEntity(patient);
        View.WorldRotation = Angle.Zero;
    }

    public void SetOrgans(bool organs, byte[] present)
    {
        _organs = organs;
        _present = present;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        Read(args.RelativePosition);
        Down = true;
        Inside = true;
        _held = true;
        _dragFrom = args.RelativePosition;
        if (_organs && Hit(args.RelativePosition) is { } organ)
            Picked?.Invoke(organ);

        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        Down = false;
        Read(args.RelativePosition);
        if (_held && _organs && (args.RelativePosition - _dragFrom).Length() > 28f)
            Dragged?.Invoke();

        _held = false;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        Inside = true;
        Read(args.RelativePosition);
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        Inside = false;
        Down = false;
        _held = false;
    }

    private void Read(Vector2 point)
    {
        if (View.PixelSize.X < 8)
            return;

        var center = View.Position + View.PixelSize / 2f;
        var pixel = (point - center) / ViewScale;
        Meters = new Vector2(pixel.X, -pixel.Y) / 32f;
    }

    private byte? Hit(Vector2 point)
    {
        if (View.PixelSize.X < 8)
            return null;

        var center = View.Position + View.PixelSize / 2f;
        var pixel = (point - center) / ViewScale + new Vector2(16f, 16f);
        foreach (var spot in Spots(_present))
        {
            if (!Has(spot.Organ))
                continue;

            if (pixel.X >= spot.X && pixel.X <= spot.X + spot.W && pixel.Y >= spot.Y && pixel.Y <= spot.Y + spot.H)
                return (byte) spot.Organ;
        }

        return null;
    }

    private bool Has(LimbOrgan organ)
    {
        foreach (var item in _present)
        {
            if (item == (byte) organ)
                return true;
        }

        return false;
    }

    private readonly record struct OrganSpot(LimbOrgan Organ, float X, float Y, float W, float H);

    private static IEnumerable<OrganSpot> Spots(byte[] present)
    {
        foreach (var organ in IoCManager.Resolve<IPrototypeManager>().EnumeratePrototypes<SurgeryOrganPrototype>())
        {
            if (!Enum.TryParse<LimbOrgan>(organ.ID, out var kind))
                continue;

            var shown = false;
            foreach (var item in present)
            {
                if (item == (byte) kind)
                    shown = true;
            }

            if (!shown)
                continue;

            foreach (var spot in organ.Spots)
            {
                if (spot.HitW <= 0 || spot.HitH <= 0)
                    continue;

                yield return new OrganSpot(kind, spot.HitX, spot.HitY, spot.HitW, spot.HitH);
            }
        }
    }
}

public sealed class SurgeryHint : Control
{
    private const float Icon = 20f;
    private const float Gap = 4f;
    private const float Pad = 6f;

    private readonly List<(string Path, string State)> _shown = new();

    public SurgeryHint()
    {
        MinSize = new Vector2(Pad * 2 + Icon, Pad * 2 + Icon);
        MouseFilter = MouseFilterMode.Ignore;
    }

    public void Show(SurgeryVerbKind verb, SurgeryVerbKind undo = SurgeryVerbKind.Inspect)
    {
        _shown.Clear();
        Add(verb);
        if (undo != SurgeryVerbKind.Inspect && undo != verb)
            Add(undo);

        var count = Math.Max(1, _shown.Count);
        MinSize = new Vector2(Pad * 2 + count * Icon + (count - 1) * Gap, Pad * 2 + Icon);
    }

    private void Add(SurgeryVerbKind verb)
    {
        if (!IoCManager.Resolve<IPrototypeManager>().TryIndex<SurgeryHintPrototype>(verb.ToString(), out var hint))
            return;

        foreach (var icon in hint.Icons)
        {
            _shown.Add((icon.Sprite, icon.State));
            if (_shown.Count == 4)
                break;
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (_shown.Count == 0)
            return;

        var size = Size.X > 2f ? Size : MinSize;
        var radius = size.Y / 2f;
        var fill = new Color(0.96f, 0.97f, 0.98f, 0.94f);
        if (size.X > size.Y + 1f)
        {
            handle.DrawRect(new UIBox2(radius, 0f, size.X - radius, size.Y), fill);
            handle.DrawCircle(new Vector2(radius, radius), radius, fill);
            handle.DrawCircle(new Vector2(size.X - radius, radius), radius, fill);
        }
        else
        {
            handle.DrawCircle(size / 2f, radius, fill);
        }

        for (var i = 0; i < _shown.Count; i++)
        {
            var tool = _shown[i];
            var rsi = IoCManager.Resolve<IResourceCache>().GetResource<RSIResource>(tool.Path).RSI;
            if (!rsi.TryGetState(tool.State, out var state))
                continue;

            var box = UIBox2.FromDimensions(new Vector2(Pad + i * (Icon + Gap), Pad), new Vector2(Icon, Icon));
            handle.DrawTextureRect(state.GetFrame(RsiDirection.South, 0), box, Color.White);
        }
    }
}
