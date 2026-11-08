// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.Stylesheets;
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
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Surgery;

public sealed class SurgeryChestMap : Control
{
    public event Action<byte>? Picked;

    public bool Sever;
    public bool Insert;

    private readonly record struct Spot(LimbOrgan Organ, string State, int X, int Y, int W, int H, float Cx, float Cy, float Height);

    private readonly List<Spot> _spots = new();
    private byte[] _present = Array.Empty<byte>();
    private byte[] _missing = Array.Empty<byte>();
    private byte _chosen = 255;
    private LimbOrgan? _hover;
    private RSI? _organs;
    private const string HumanOrgans = "/Textures/Mobs/Species/Human/organs.rsi";
    private string _organsPath = HumanOrgans;
    private string _species = "";
    private bool _machine;

    public SurgeryChestMap()
    {
        MouseFilter = MouseFilterMode.Stop;
        MinSize = new Vector2(170, 200);
    }

    public void Show(byte[] organs, byte[] missing, byte chosen, bool sever, bool insert, LimbZone zone, string species = "")
    {
        _present = organs;
        _missing = missing;
        _chosen = chosen;
        Sever = sever;
        Insert = insert;
        _hover = null;
        _spots.Clear();
        _species = species;
        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        prototypes.TryIndex<SurgerySpeciesPrototype>(species, out var look);
        _machine = look is { Machine: true };
        var sprite = look != null && look.OrganSprite.Length > 0 ? look.OrganSprite : HumanOrgans;
        if (sprite != _organsPath)
        {
            _organsPath = sprite;
            _organs = null;
        }

        foreach (var organ in prototypes.EnumeratePrototypes<SurgeryOrganPrototype>())
        {
            var machine = look is { Machine: true };
            if (!organ.InZone(zone, machine) || !Enum.TryParse<LimbOrgan>(organ.ID, out var kind))
                continue;

            if (!Has(kind) && !Missing(kind))
                continue;

            var state = look != null && look.OrganStates.TryGetValue(organ.ID, out var own) ? own : null;
            var spots = look != null && look.OrganSpots.TryGetValue(organ.ID, out var placed) ? placed : organ.SpotsFor(machine);
            foreach (var spot in spots)
                _spots.Add(new Spot(kind, state ?? spot.State, spot.X, spot.Y, spot.W, spot.H, spot.Cx, spot.Cy, spot.Height));
        }
    }

    public event Action? Stanched;

    private readonly List<(Vector2 At, float Left)> _bleeds = new();
    private bool _bleedHeld;
    private bool _stanched;
    private Vector2 _mouse;
    private float _pulse;

    public void Bleed(int count)
    {
        if (_bleeds.Count > 0)
            return;

        _stanched = false;
        for (var i = 0; i < Math.Clamp(count, 1, 5); i++)
            _bleeds.Add((new Vector2(0.2f + Random.Shared.NextSingle() * 0.6f, 0.2f + Random.Shared.NextSingle() * 0.6f), 1f));
    }

    public void StopBleed()
    {
        _bleeds.Clear();
        _bleedHeld = false;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _pulse += args.DeltaSeconds;
        if (_bleeds.Count == 0 || _stanched)
            return;

        var left = false;
        for (var i = 0; i < _bleeds.Count; i++)
        {
            var (at, amount) = _bleeds[i];
            if (amount <= 0f)
                continue;

            if (_bleedHeld && (BleedAt(at, PixelSize) - _mouse).Length() < (10f + 10f * amount) * UIScale)
                amount = MathF.Max(0f, amount - args.DeltaSeconds / 0.6f);

            _bleeds[i] = (at, amount);
            left |= amount > 0f;
        }

        if (left)
            return;

        _stanched = true;
        Stanched?.Invoke();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function == EngineKeyFunctions.UIClick)
            _bleedHeld = false;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        if (_bleeds.Count > 0)
        {
            _bleedHeld = true;
            _mouse = args.RelativePosition * UIScale;
            args.Handle();
            return;
        }

        var organ = Hit(args.RelativePosition);
        if (Lodged != null && _lodgedBox.Width > 1f && _lodgedBox.Contains(args.RelativePosition * UIScale))
        {
            TakeLodged?.Invoke();
            args.Handle();
            return;
        }

        if (Shard != null && _shardBox.Width > 1f && _shardBox.Contains(args.RelativePosition * UIScale))
        {
            TakeShard?.Invoke();
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
        _mouse = args.RelativePosition * UIScale;
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _bleedHeld = false;
    }

    private static readonly Color Accent = new(0.78f, 0.27f, 0.27f, 0.9f);
    private static readonly Color Cavity = new(0.22f, 0.07f, 0.08f, 1f);
    private static readonly Color Take = new(0.86f, 0.24f, 0.2f, 1f);
    private static readonly Color Put = new(0.3f, 0.82f, 0.38f, 1f);
    private Font? _font;

    protected override void Draw(DrawingHandleScreen handle)
    {
        var size = PixelSize;
        var scale = UIScale;
        var outer = new UIBox2(Vector2.Zero, size);
        RoundRect(handle, outer, 12f * scale, Accent);
        RoundRect(handle, Shrink(outer, 2f * scale), 10f * scale, SurgeryHint.Back);
        var cavity = CavityBox(size);
        RoundRect(handle, cavity, 8f * scale, Cavity);

        foreach (var spot in _spots)
        {
            if (!Sheet().TryGetState(spot.State, out var state))
                continue;

            var missing = Missing(spot.Organ);
            var rect = OrganRect(spot, cavity);
            var lit = _hover == spot.Organ || _chosen == (byte) spot.Organ && (Sever || missing && Insert);
            if (lit && (!missing || Insert))
            {
                var glow = missing ? Put : Take;
                var radius = MathF.Max(rect.Width, rect.Height) * 0.62f;
                handle.DrawCircle(rect.Center, radius + 2f * scale, glow.WithAlpha(0.85f));
                handle.DrawCircle(rect.Center, radius, new Color(0.14f, 0.16f, 0.19f, 0.92f));
            }

            var ink = missing
                ? lit && Insert ? new Color(0.78f, 0.8f, 0.84f, 0.9f) : new Color(0.42f, 0.44f, 0.48f, 0.55f)
                : Color.White;
            var frame = state.GetFrame(RsiDirection.South, 0);
            if (_machine)
            {
                handle.DrawTextureRectRegion(
                    frame,
                    rect,
                    UIBox2.FromDimensions(new Vector2(spot.X, spot.Y), new Vector2(spot.W, spot.H)),
                    ink);
            }
            else
            {
                var texel = rect.Height / Math.Max(spot.H, 1);
                var origin = new Vector2(rect.Left - spot.X * texel, rect.Top - spot.Y * texel);
                handle.DrawTextureRect(frame, UIBox2.FromDimensions(origin, new Vector2(32, 32) * texel), ink);
            }
        }

        var item = 30f * scale;
        var both = Lodged != null && Shard != null;
        _lodgedBox = default;
        _shardBox = default;
        if (Lodged != null)
            _lodgedBox = ItemBox(handle, Lodged, cavity, item, both ? -0.75f : 0f);
        if (Shard != null)
            _shardBox = ItemBox(handle, Shard, cavity, item, both ? 0.75f : 0f);

        var beat = 1f + 0.18f * MathF.Sin(_pulse * 7f);
        foreach (var (at, left) in _bleeds)
        {
            if (left <= 0f)
                continue;

            var center = BleedAt(at, size);
            handle.DrawCircle(center, (6f + 9f * left) * beat * scale, new Color(0.75f, 0.02f, 0.04f, 0.85f));
            handle.DrawCircle(center, (3f + 4f * left) * beat * scale, new Color(1f, 0.25f, 0.2f, 0.95f));
        }

        if (_hover is { } hover)
            Label(handle, SurgerySpeciesPrototype.OrganName(IoCManager.Resolve<IPrototypeManager>(), _species, hover.ToString()), Missing(hover) ? Put : Take, new Vector2(size.X / 2f, size.Y - 6f * scale));
        else if (Shard != null && _shardBox.Contains(_mouse))
            Label(handle, Loc.GetString("surgery-cavity-shard"), Take, new Vector2(size.X / 2f, size.Y - 6f * scale));
    }

    private static UIBox2 ItemBox(DrawingHandleScreen handle, Texture icon, UIBox2 cavity, float box, float shift)
    {
        var at = UIBox2.FromDimensions(new Vector2(cavity.Center.X - box / 2f + shift * box, cavity.Bottom - box * 1.13f), new Vector2(box, box));
        handle.DrawCircle(at.Center, box * 0.62f, Take.WithAlpha(0.85f));
        handle.DrawCircle(at.Center, box * 0.56f, new Color(0.14f, 0.16f, 0.19f, 0.92f));
        handle.DrawTextureRect(icon, at, Color.White);
        return at;
    }

    private void Label(DrawingHandleScreen handle, string text, Color color, Vector2 bottom)
    {
        _font ??= IoCManager.Resolve<IResourceCache>().NotoStack(variation: "Bold", size: 10);
        var width = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            if (_font.TryGetCharMetrics(rune, 1f, out var metrics))
                width += metrics.Advance;
        }

        var height = _font.GetLineHeight(1f) + 8f;
        var box = UIBox2.FromDimensions(new Vector2(bottom.X - (width + 18f) / 2f, bottom.Y - height), new Vector2(width + 18f, height));
        RoundRect(handle, box, height / 2f, color.WithAlpha(0.9f));
        RoundRect(handle, Shrink(box, 2f), height / 2f - 2f, SurgeryHint.Back);
        handle.DrawString(_font, new Vector2(box.Left + 9f, box.Top + 4f), text, color);
    }

    private static UIBox2 Shrink(UIBox2 box, float by)
    {
        return new UIBox2(box.Left + by, box.Top + by, box.Right - by, box.Bottom - by);
    }

    private static void RoundRect(DrawingHandleScreen handle, UIBox2 box, float radius, Color fill)
    {
        radius = MathF.Max(0f, MathF.Min(radius, MathF.Min(box.Width, box.Height) / 2f));
        handle.DrawRect(new UIBox2(box.Left + radius, box.Top, box.Right - radius, box.Bottom), fill);
        handle.DrawRect(new UIBox2(box.Left, box.Top + radius, box.Right, box.Bottom - radius), fill);
        handle.DrawCircle(new Vector2(box.Left + radius, box.Top + radius), radius, fill);
        handle.DrawCircle(new Vector2(box.Right - radius, box.Top + radius), radius, fill);
        handle.DrawCircle(new Vector2(box.Left + radius, box.Bottom - radius), radius, fill);
        handle.DrawCircle(new Vector2(box.Right - radius, box.Bottom - radius), radius, fill);
    }

    private UIBox2 CavityBox(Vector2 size)
    {
        var pad = 10f * UIScale;
        return new UIBox2(pad, pad, size.X - pad, size.Y - pad - 26f * UIScale);
    }

    private Vector2 BleedAt(Vector2 at, Vector2 size)
    {
        var cavity = CavityBox(size);
        return cavity.TopLeft + at * cavity.Size;
    }

    public event Action? TakeLodged;
    public Texture? Lodged;
    private UIBox2 _lodgedBox;

    public event Action? TakeShard;
    public Texture? Shard;
    private UIBox2 _shardBox;

    private UIBox2 OrganRect(Spot spot, UIBox2 cavity)
    {
        var height = cavity.Height * spot.Height;
        var width = height * spot.W / Math.Max(spot.H, 1);
        var center = cavity.TopLeft + new Vector2(cavity.Width * spot.Cx, cavity.Height * spot.Cy);
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
        var cavity = CavityBox(PixelSize);
        var pixel = point * UIScale;
        for (var i = _spots.Count - 1; i >= 0; i--)
        {
            var spot = _spots[i];
            if (!OrganRect(spot, cavity).Contains(pixel))
                continue;

            if (Insert)
            {
                if (Missing(spot.Organ))
                    return spot.Organ;

                continue;
            }

            if (Has(spot.Organ))
                return spot.Organ;
        }

        return null;
    }

    private RSI Sheet()
    {
        return _organs ??= IoCManager.Resolve<IResourceCache>().GetResource<RSIResource>(_organsPath).RSI;
    }
}

public sealed class SurgeryHint : Control
{
    private const float Icon = 26f;
    private const float Disc = 17f;
    private const float Pad = 7f;
    private const float Gap = 8f;
    private const float HintHeight = Pad * 2f + Disc * 2f;

    public static readonly Color Back = new(0.07f, 0.09f, 0.11f, 0.93f);

    private readonly List<(Texture? Icon, string Text, Color Color)> _shown = new();
    private readonly Font _font;
    private readonly Font _small;

    public SurgeryHint()
    {
        var cache = IoCManager.Resolve<IResourceCache>();
        _font = cache.NotoStack(variation: "Bold", size: 11);
        _small = cache.NotoStack(size: 9);
        MinSize = new Vector2(Pad * 2f + Disc * 2f, HintHeight);
        MouseFilter = MouseFilterMode.Ignore;
    }

    public void Show(SurgeryVerbKind verb, SurgeryVerbKind undo = SurgeryVerbKind.Inspect, string hint = "", string suffix = "", string extra = "", bool machine = false, bool soft = false)
    {
        _shown.Clear();
        Add(hint.Length > 0 ? hint : verb.ToString(), suffix, machine, soft);
        if (extra.Length > 0)
            Add(extra, "", machine, soft);
        else if (undo != SurgeryVerbKind.Inspect && undo != verb)
            Add(undo == SurgeryVerbKind.Retract ? "RetractClose" : undo.ToString(), "", machine, soft);

        var width = Pad;
        for (var i = 0; i < _shown.Count; i++)
        {
            if (i > 0)
                width += Gap + Measure(_small, Loc.GetString("surgery-hint-or")) + Gap;
            width += Disc * 2f + 6f + Measure(_font, _shown[i].Text);
        }

        MinSize = new Vector2(MathF.Max(Pad * 2f + Disc * 2f, width + Pad), HintHeight);
    }

    private void Add(string id, string suffix, bool machine = false, bool soft = false)
    {
        if (soft && id == nameof(SurgeryVerbKind.MendBone))
            id = "SlimeStir";

        if (!IoCManager.Resolve<IPrototypeManager>().TryIndex<SurgeryHintPrototype>(id, out var hint))
            return;

        Texture? icon = null;
        var icons = machine && hint.MachineIcons.Count > 0 ? hint.MachineIcons : hint.Icons;
        if (icons.Count > 0)
        {
            var rsi = IoCManager.Resolve<IResourceCache>().GetResource<RSIResource>(SurgeryClientSystem.TexturePath(icons[0].Sprite)).RSI;
            if (rsi.TryGetState(icons[0].State, out var state))
                icon = state.GetFrame(RsiDirection.South, 0);
        }

        var text = hint.Name.Length > 0 ? Loc.GetString(hint.Name) : id;
        _shown.Add((icon, text + suffix, hint.Color));
    }

    private static float Measure(Font font, string text)
    {
        var width = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            if (font.TryGetCharMetrics(rune, 1f, out var metrics))
                width += metrics.Advance;
        }

        return width;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (_shown.Count == 0)
            return;

        var size = Size.X > 2f ? Size : MinSize;
        var radius = size.Y / 2f;
        var accent = _shown[0].Color;
        Capsule(handle, Vector2.Zero, size, accent.WithAlpha(0.9f));
        Capsule(handle, new Vector2(2f, 2f), size - new Vector2(4f, 4f), Back);

        var x = Pad;
        for (var i = 0; i < _shown.Count; i++)
        {
            if (i > 0)
            {
                var or = Loc.GetString("surgery-hint-or");
                x += Gap;
                handle.DrawString(_small, new Vector2(x, radius - _small.GetLineHeight(1f) / 2f), or, new Color(0.6f, 0.64f, 0.68f));
                x += Measure(_small, or) + Gap;
            }

            var (icon, text, color) = _shown[i];
            var center = new Vector2(x + Disc, radius);
            handle.DrawCircle(center, Disc, color.WithAlpha(0.95f));
            handle.DrawCircle(center, Disc - 2.5f, new Color(0.14f, 0.16f, 0.19f, 1f));
            if (icon != null)
                handle.DrawTextureRect(icon, UIBox2.FromDimensions(center - new Vector2(Icon, Icon) / 2f, new Vector2(Icon, Icon)), Color.White);

            x += Disc * 2f + 6f;
            handle.DrawString(_font, new Vector2(x, radius - _font.GetLineHeight(1f) / 2f), text, color);
            x += Measure(_font, text);
        }
    }

    private static void Capsule(DrawingHandleScreen handle, Vector2 at, Vector2 size, Color fill)
    {
        var radius = size.Y / 2f;
        handle.DrawRect(UIBox2.FromDimensions(at + new Vector2(radius, 0f), new Vector2(MathF.Max(0f, size.X - radius * 2f), size.Y)), fill);
        handle.DrawCircle(at + new Vector2(radius, radius), radius, fill);
        handle.DrawCircle(at + new Vector2(size.X - radius, radius), radius, fill);
    }
}

public enum SurgeryFanBadge : byte
{
    None,
    Take,
    Put,
}

/// <param name="Text">Short label drawn in the disc instead of <paramref name="Layers"/>.</param>
public sealed record SurgeryFanIcon(List<Texture> Layers, SurgeryFanBadge Badge, Color Color, string Name, string? Text = null);

public sealed class SurgeryFan : Control
{
    public event Action<int>? Picked;
    public event Action<int>? Hovered;

    private static readonly Color Ray = new(0.55f, 0.78f, 0.86f, 0.35f);
    private static readonly Color Inner = new(0.14f, 0.16f, 0.19f, 1f);

    private readonly List<SurgeryFanIcon> _icons = new();
    private readonly Font _font;
    private readonly Font _small;
    private int _hover = -1;

    public SurgeryFan()
    {
        MouseFilter = MouseFilterMode.Stop;
        MinSize = new Vector2(220, 170);
        var cache = IoCManager.Resolve<IResourceCache>();
        _font = cache.NotoStack(variation: "Bold", size: 11);
        _small = cache.NotoStack(variation: "Bold", size: 9);
    }

    private const int RingFrom = 4;
    private const float IconSize = 36f;
    private const float IconGap = 22f;
    private const float Disc = 21f;

    public bool Ring => _icons.Count >= RingFrom;

    public Vector2 Origin => Ring ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0.92f);

    public void SetIcons(List<SurgeryFanIcon> icons)
    {
        _icons.Clear();
        _icons.AddRange(icons);
        if (Ring)
        {
            var side = (RingRadius() + IconSize) * 2f + 12f;
            MinSize = new Vector2(side, side);
        }
        else
        {
            MinSize = new Vector2(220, 170);
        }
    }

    private float RingRadius()
    {
        return MathF.Max(62f, _icons.Count * (IconSize + IconGap) / (2f * MathF.PI));
    }

    private Vector2 At(int index, Vector2 size)
    {
        var count = Math.Max(1, _icons.Count);
        if (Ring)
        {
            var scale = size.X / MathF.Max(1f, MinSize.X);
            var ring = MathF.PI * 2f * index / count - MathF.PI / 2f;
            return size / 2f + new Vector2(MathF.Cos(ring), MathF.Sin(ring)) * RingRadius() * scale;
        }

        var along = count == 1 ? 0.5f : index / (float) (count - 1);
        var angle = MathF.PI * (FanFrom - (FanFrom - FanTo) * along);
        return new Vector2(size.X * 0.5f + MathF.Cos(angle) * size.X * 0.36f, size.Y * 0.86f - MathF.Sin(angle) * size.Y * 0.62f);
    }

    private const float FanFrom = 0.82f;
    private const float FanTo = 0.18f;

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (Ring)
        {
            DrawRing(handle);
            return;
        }

        var origin = PixelSize * Origin;
        handle.DrawCircle(origin, 4f * UIScale, SurgeryHint.Back);
        handle.DrawCircle(origin, 2.5f * UIScale, Ray.WithAlpha(0.9f));
        for (var i = 0; i < _icons.Count; i++)
            handle.DrawLine(origin, At(i, PixelSize), Ray);

        for (var i = 0; i < _icons.Count; i++)
            DrawIcon(handle, i, At(i, PixelSize));

        Label(handle, new Vector2(PixelSize.X * 0.5f, 4f * UIScale), false);
    }

    private void DrawRing(DrawingHandleScreen handle)
    {
        var center = PixelSize / 2f;
        handle.DrawCircle(center, 4f * UIScale, SurgeryHint.Back);
        handle.DrawCircle(center, 2.5f * UIScale, Ray.WithAlpha(0.9f));
        for (var i = 0; i < _icons.Count; i++)
            handle.DrawLine(center, At(i, PixelSize), Ray);

        for (var i = 0; i < _icons.Count; i++)
            DrawIcon(handle, i, At(i, PixelSize));

        Label(handle, center, true);
    }

    private void Label(DrawingHandleScreen handle, Vector2 at, bool middle)
    {
        if ((uint) _hover >= (uint) _icons.Count || _icons[_hover].Name.Length == 0)
            return;

        var icon = _icons[_hover];
        var width = 0f;
        foreach (var rune in icon.Name.EnumerateRunes())
        {
            if (_font.TryGetCharMetrics(rune, 1f, out var metrics))
                width += metrics.Advance;
        }

        var line = _font.GetLineHeight(1f);
        var size = new Vector2(width + 22f, line + 10f);
        var top = middle ? at.Y - size.Y / 2f : at.Y;
        var left = at.X - size.X / 2f;
        Capsule(handle, new Vector2(left, top), size, icon.Color.WithAlpha(0.9f));
        Capsule(handle, new Vector2(left + 2f, top + 2f), size - new Vector2(4f, 4f), SurgeryHint.Back);
        handle.DrawString(_font, new Vector2(left + 11f, top + 5f), icon.Name, icon.Color);
    }

    private static void Capsule(DrawingHandleScreen handle, Vector2 at, Vector2 size, Color fill)
    {
        var radius = size.Y / 2f;
        handle.DrawRect(UIBox2.FromDimensions(at + new Vector2(radius, 0f), new Vector2(MathF.Max(0f, size.X - radius * 2f), size.Y)), fill);
        handle.DrawCircle(at + new Vector2(radius, radius), radius, fill);
        handle.DrawCircle(at + new Vector2(size.X - radius, radius), radius, fill);
    }

    private void DrawIcon(DrawingHandleScreen handle, int index, Vector2 at)
    {
        var icon = _icons[index];
        var scale = UIScale;
        var hovered = index == _hover;
        var disc = Disc * scale * (hovered ? 1.15f : 1f) * (icon.Text != null ? 1.25f : 1f);
        handle.DrawCircle(at, disc + 2f * scale, SurgeryHint.Back);
        handle.DrawCircle(at, disc, icon.Color.WithAlpha(hovered ? 1f : 0.85f));
        handle.DrawCircle(at, disc - (hovered ? 3.5f : 2.5f) * scale, Inner);
        if (icon.Text != null)
        {
            DrawLabel(handle, at, icon.Text, icon.Color);
            return;
        }
        var half = 15f * scale * (hovered ? 1.1f : 1f);
        var box = UIBox2.FromDimensions(at - new Vector2(half, half), new Vector2(half * 2f, half * 2f));

        foreach (var layer in icon.Layers)
            handle.DrawTextureRect(layer, box, Color.White);

        if (icon.Badge == SurgeryFanBadge.None)
            return;

        var corner = new Vector2(box.Right - 6f, box.Top + 5f);
        var ink = icon.Badge == SurgeryFanBadge.Take ? new Color(0.86f, 0.16f, 0.14f, 1f) : new Color(0.2f, 0.78f, 0.3f, 1f);
        handle.DrawRect(UIBox2.FromDimensions(corner - new Vector2(5f, 1.5f), new Vector2(10f, 3f)), ink);
        if (icon.Badge == SurgeryFanBadge.Put)
            handle.DrawRect(UIBox2.FromDimensions(corner - new Vector2(1.5f, 5f), new Vector2(3f, 10f)), ink);
    }

    private void DrawLabel(DrawingHandleScreen handle, Vector2 center, string text, Color color)
    {
        var lines = text.Split(' ', 2);
        var height = _small.GetLineHeight(UIScale);
        var top = center.Y - height * lines.Length / 2f;
        foreach (var line in lines)
        {
            var width = 0f;
            foreach (var rune in line.EnumerateRunes())
            {
                if (_small.TryGetCharMetrics(rune, UIScale, out var metrics))
                    width += metrics.Advance;
            }

            handle.DrawString(_small, new Vector2(center.X - width / 2f, top), line, UIScale, Color.InterpolateBetween(color, Color.White, 0.6f));
            top += height;
        }
    }

    private int Hit(Vector2 point)
    {
        for (var i = 0; i < _icons.Count; i++)
        {
            var reach = 26f * UIScale;
            if ((point - At(i, PixelSize)).LengthSquared() <= reach * reach)
                return i;
        }

        return -1;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        var hit = Hit(args.RelativePosition * UIScale);
        if (hit == _hover)
            return;

        _hover = hit;
        Hovered?.Invoke(hit);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        var hit = Hit(args.RelativePosition * UIScale);
        if (hit < 0)
            return;

        Picked?.Invoke(hit);
        args.Handle();
    }
}
