// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Client.Resources;
using Content.Shared.DeadSpace.OrganDamage;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Client.DeadSpace.Microscope.UI;

public sealed class OrganView : LayoutContainer
{
    private const float Zoom = 2.0f;
    private const float BackGrow = 1.06f;
    private const float BackShift = 7f;
    private const float BackDepthFactor = 0.55f;

    private static readonly Dictionary<string, Color> WoundColors = new()
    {
        ["Blunt"] = Color.FromHex("#a83232"),
        ["Slash"] = Color.FromHex("#b84141"),
        ["Piercing"] = Color.FromHex("#8f2b2b"),
        ["Heat"] = Color.FromHex("#c96a1f"),
        ["Shock"] = Color.FromHex("#c9b41f"),
        ["Cold"] = Color.FromHex("#4f9fd6"),
        ["Poison"] = Color.FromHex("#4fae4f"),
        ["Caustic"] = Color.FromHex("#8fbf3f"),
        ["Asphyxiation"] = Color.FromHex("#3f6fd6"),
        ["Bloodloss"] = Color.FromHex("#7c1f1f"),
        ["Cellular"] = Color.FromHex("#8a4fd6"),
        ["Radiation"] = Color.FromHex("#9fe066"),
    };

    private static readonly Vector2[] FallbackAnchors = { new(0.46f, 0.5f), new(0.54f, 0.5f) };

    private static readonly Dictionary<string, Vector2[]> StateAnchors = new()
    {
        ["appendix-inflamed"] = new Vector2[] { new Vector2(0.484f, 0.359f), new Vector2(0.547f, 0.328f), new Vector2(0.609f, 0.391f), new Vector2(0.484f, 0.484f), new Vector2(0.547f, 0.484f), new Vector2(0.609f, 0.547f), new Vector2(0.484f, 0.609f), new Vector2(0.578f, 0.672f), new Vector2(0.609f, 0.609f), new Vector2(0.544f, 0.502f) },
        ["appendix"] = new Vector2[] { new Vector2(0.484f, 0.359f), new Vector2(0.516f, 0.359f), new Vector2(0.578f, 0.359f), new Vector2(0.453f, 0.484f), new Vector2(0.516f, 0.484f), new Vector2(0.484f, 0.578f), new Vector2(0.516f, 0.578f), new Vector2(0.578f, 0.641f), new Vector2(0.516f, 0.482f) },
        ["brain"] = new Vector2[] { new Vector2(0.359f, 0.359f), new Vector2(0.547f, 0.422f), new Vector2(0.641f, 0.391f), new Vector2(0.359f, 0.453f), new Vector2(0.484f, 0.453f), new Vector2(0.578f, 0.516f), new Vector2(0.516f, 0.609f), new Vector2(0.609f, 0.578f), new Vector2(0.497f, 0.468f) },
        ["ears"] = new Vector2[] { new Vector2(0.266f, 0.359f), new Vector2(0.516f, 0.359f), new Vector2(0.359f, 0.484f), new Vector2(0.516f, 0.547f), new Vector2(0.609f, 0.609f), new Vector2(0.359f, 0.641f), new Vector2(0.578f, 0.703f), new Vector2(0.641f, 0.797f), new Vector2(0.498f, 0.558f) },
        ["eyeball-l"] = new Vector2[] { new Vector2(0.297f, 0.266f), new Vector2(0.359f, 0.359f), new Vector2(0.484f, 0.359f), new Vector2(0.203f, 0.484f), new Vector2(0.422f, 0.453f), new Vector2(0.484f, 0.391f), new Vector2(0.234f, 0.547f), new Vector2(0.328f, 0.578f), new Vector2(0.453f, 0.516f), new Vector2(0.348f, 0.442f) },
        ["eyeball-r"] = new Vector2[] { new Vector2(0.703f, 0.328f), new Vector2(0.797f, 0.328f), new Vector2(0.578f, 0.547f), new Vector2(0.734f, 0.453f), new Vector2(0.828f, 0.453f), new Vector2(0.578f, 0.609f), new Vector2(0.672f, 0.672f), new Vector2(0.662f, 0.552f) },
        ["heart-off"] = new Vector2[] { new Vector2(0.391f, 0.297f), new Vector2(0.422f, 0.359f), new Vector2(0.484f, 0.328f), new Vector2(0.359f, 0.422f), new Vector2(0.422f, 0.453f), new Vector2(0.484f, 0.453f), new Vector2(0.391f, 0.484f), new Vector2(0.453f, 0.516f), new Vector2(0.484f, 0.484f), new Vector2(0.438f, 0.431f) },
        ["heart-on"] = new Vector2[] { new Vector2(0.180f, 0.227f), new Vector2(0.695f, 0.148f), new Vector2(0.211f, 0.680f), new Vector2(0.395f, 0.378f) },
        ["kidney-l"] = new Vector2[] { new Vector2(0.328f, 0.453f), new Vector2(0.391f, 0.422f), new Vector2(0.453f, 0.328f), new Vector2(0.297f, 0.516f), new Vector2(0.422f, 0.547f), new Vector2(0.453f, 0.578f), new Vector2(0.297f, 0.609f), new Vector2(0.391f, 0.641f), new Vector2(0.453f, 0.641f), new Vector2(0.371f, 0.515f) },
        ["kidney-r"] = new Vector2[] { new Vector2(0.516f, 0.391f), new Vector2(0.641f, 0.391f), new Vector2(0.672f, 0.422f), new Vector2(0.547f, 0.578f), new Vector2(0.641f, 0.484f), new Vector2(0.703f, 0.547f), new Vector2(0.516f, 0.703f), new Vector2(0.609f, 0.641f), new Vector2(0.672f, 0.609f), new Vector2(0.598f, 0.518f) },
        ["liver"] = new Vector2[] { new Vector2(0.391f, 0.453f), new Vector2(0.547f, 0.484f), new Vector2(0.609f, 0.391f), new Vector2(0.328f, 0.578f), new Vector2(0.484f, 0.547f), new Vector2(0.641f, 0.516f), new Vector2(0.422f, 0.672f), new Vector2(0.453f, 0.641f), new Vector2(0.473f, 0.514f) },
        ["lung-l"] = new Vector2[] { new Vector2(0.359f, 0.422f), new Vector2(0.391f, 0.391f), new Vector2(0.453f, 0.422f), new Vector2(0.359f, 0.578f), new Vector2(0.422f, 0.516f), new Vector2(0.453f, 0.484f), new Vector2(0.359f, 0.609f), new Vector2(0.422f, 0.609f), new Vector2(0.453f, 0.609f), new Vector2(0.398f, 0.535f) },
        ["lung-r"] = new Vector2[] { new Vector2(0.516f, 0.391f), new Vector2(0.578f, 0.391f), new Vector2(0.484f, 0.547f), new Vector2(0.578f, 0.547f), new Vector2(0.672f, 0.547f), new Vector2(0.516f, 0.672f), new Vector2(0.578f, 0.609f), new Vector2(0.672f, 0.641f), new Vector2(0.571f, 0.535f) },
        ["muscle"] = new Vector2[] { new Vector2(0.453f, 0.391f), new Vector2(0.484f, 0.391f), new Vector2(0.672f, 0.359f), new Vector2(0.453f, 0.547f), new Vector2(0.516f, 0.516f), new Vector2(0.641f, 0.453f), new Vector2(0.453f, 0.672f), new Vector2(0.484f, 0.578f), new Vector2(0.609f, 0.578f), new Vector2(0.537f, 0.484f) },
        ["nerve"] = new Vector2[] { new Vector2(0.453f, 0.484f), new Vector2(0.516f, 0.453f), new Vector2(0.672f, 0.453f), new Vector2(0.359f, 0.609f), new Vector2(0.516f, 0.547f), new Vector2(0.672f, 0.516f), new Vector2(0.422f, 0.641f), new Vector2(0.547f, 0.641f), new Vector2(0.525f, 0.524f) },
        ["stomach"] = new Vector2[] { new Vector2(0.484f, 0.297f), new Vector2(0.641f, 0.359f), new Vector2(0.391f, 0.547f), new Vector2(0.547f, 0.391f), new Vector2(0.578f, 0.516f), new Vector2(0.359f, 0.609f), new Vector2(0.516f, 0.672f), new Vector2(0.578f, 0.641f), new Vector2(0.516f, 0.509f) },
        ["tongue"] = new Vector2[] { new Vector2(0.422f, 0.547f), new Vector2(0.484f, 0.484f), new Vector2(0.578f, 0.516f), new Vector2(0.391f, 0.578f), new Vector2(0.484f, 0.609f), new Vector2(0.641f, 0.609f), new Vector2(0.422f, 0.641f), new Vector2(0.547f, 0.641f), new Vector2(0.578f, 0.641f), new Vector2(0.507f, 0.578f) },
        ["vessel"] = new Vector2[] { new Vector2(0.359f, 0.359f), new Vector2(0.453f, 0.359f), new Vector2(0.641f, 0.453f), new Vector2(0.297f, 0.578f), new Vector2(0.484f, 0.516f), new Vector2(0.734f, 0.547f), new Vector2(0.391f, 0.703f), new Vector2(0.484f, 0.734f), new Vector2(0.472f, 0.522f) },
    };

    private static readonly string[] DecalTexturePaths =
    {
        "/Textures/Fluids/puddle.rsi/splat0.png",
        "/Textures/Fluids/puddle.rsi/splat2.png",
        "/Textures/Fluids/puddle.rsi/splat4.png",
        "/Textures/Fluids/puddle.rsi/splat7.png",
        "/Textures/Fluids/puddle.rsi/splata.png",
        "/Textures/Effects/crayondecals.rsi/splatter.png",
        "/Textures/Fluids/splatter.rsi/splatter-0.png",
        "/Textures/Decals/burnt.rsi/burnt1.png",
    };

    private readonly List<Texture> _decalTextures = new();
    private readonly DecalOverlay _overlay;
    private readonly IEntityManager _entMan;
    private readonly IGameTiming _timing;

    private NetEntity? _organ;
    private Texture? _organTexture;
    private Vector2 _cameraOffset;
    private Vector2[] _currentAnchors = FallbackAnchors;
    private bool _flipped;

    public bool IsFlipped => _flipped;

    public event Action<OrganWound>? OnWoundClicked;

    public void SetFlipped(bool flipped)
    {
        _flipped = flipped;
    }

    public OrganView()
    {
        IoCManager.Instance!.Resolve(ref _entMan);
        _timing = IoCManager.Instance!.Resolve<IGameTiming>();

        MouseFilter = MouseFilterMode.Pass;

        var resources = IoCManager.Instance!.Resolve<IResourceCache>();
        foreach (var path in DecalTexturePaths)
        {
            try
            {
                _decalTextures.Add(resources.GetTexture(path));
            }
            catch (Exception)
            {
            }
        }

        _overlay = new DecalOverlay(this)
        {
            MouseFilter = MouseFilterMode.Stop,
        };

        _overlay.OnKeyBindDown += OnOverlayKeyBindDown;
        _overlay.OnMouseMoved += HandleMouseMove;

        SetAnchorPreset(_overlay, LayoutPreset.Wide);
        AddChild(_overlay);
    }

    public void SetOrgan(NetEntity? organ, List<OrganWound> wounds)
    {
        _organ = organ;
        _organTexture = null;
        _overlay.SetWounds(wounds);
        _currentAnchors = FallbackAnchors;

        if (organ == null)
        {
            _cameraOffset = Vector2.Zero;
            return;
        }

        var uid = _entMan.GetEntity(organ.Value);
        if (!_entMan.TryGetComponent<SpriteComponent>(uid, out var sprite))
        {
            _cameraOffset = Vector2.Zero;
            return;
        }

        var icon = sprite.Icon;
        if (icon != null)
            _organTexture = icon.Default;

        try
        {
            var state = sprite.LayerGetState(0);
            _currentAnchors = state.Name != null && StateAnchors.TryGetValue(state.Name, out var anchors)
                ? anchors
                : FallbackAnchors;
        }
        catch (Exception)
        {
            _currentAnchors = FallbackAnchors;
        }

        _cameraOffset = Vector2.Zero;
    }

    private void OnOverlayKeyBindDown(GUIBoundKeyEventArgs args)
    {
        if (args.Function != EngineKeyFunctions.Use)
            return;

        var wound = _overlay.HitTest(args.RelativePosition);
        if (wound != null)
            OnWoundClicked?.Invoke(wound);
    }

    private void HandleMouseMove(Vector2 pos)
    {
        if (_organ == null)
            return;

        var half = Size / 2f;
        if (half == Vector2.Zero)
            return;

        var normalized = Vector2.Clamp((pos - half) / half, new Vector2(-1f), new Vector2(1f));
        var organSide = OrganSide;
        var maxPan = (new Vector2(organSide) - Size) / 2f;

        _cameraOffset = -normalized * Vector2.Max(maxPan, Vector2.Zero);
    }

    private float OrganSide => MathF.Min(Size.X, Size.Y) * Zoom;

    private Vector2 OrganCenter(Vector2 offset) => Size / 2f + offset;

    private static Color GetWoundColor(OrganWound wound)
    {
        return WoundColors.TryGetValue(wound.DamageType, out var color) ? color : Color.Gray;
    }

    private Vector2 GetWoundScreenPos(OrganWound wound)
    {
        var rng = new Random(wound.Id.GetHashCode());
        var anchor = _currentAnchors[rng.Next(_currentAnchors.Length)];
        var jitter = new Vector2(
            (float) (rng.NextDouble() * 2.0 - 1.0) * 4f,
            (float) (rng.NextDouble() * 2.0 - 1.0) * 4f);

        if (_flipped)
            anchor = new Vector2(1f - anchor.X, anchor.Y);

        var organSide = OrganSide;
        return OrganCenter(_cameraOffset) + (anchor - new Vector2(0.5f)) * organSide + jitter;
    }

    private int AnchorIndexOf(OrganWound wound)
    {
        var rng = new Random(wound.Id.GetHashCode());
        return rng.Next(_currentAnchors.Length);
    }

    private bool IsWoundVisible(OrganWound wound)
    {
        return (AnchorIndexOf(wound) & 1) == (_flipped ? 1 : 0);
    }

    private static void DrawOrganTexture(DrawingHandleScreen handle, Texture tex, UIBox2 rect, Color tint, bool mirrored)
    {
        if (!mirrored)
        {
            handle.DrawTextureRect(tex, rect, tint);
            return;
        }

        var subRegion = new UIBox2(tex.Width, 0, 0, tex.Height);
        handle.DrawTextureRectRegion(tex, rect, subRegion, tint);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (_organ == null || _organTexture == null)
            return;

        var organSide = OrganSide;
        var organTint = Color.White;

        var organUid = _entMan.GetEntity(_organ.Value);
        if (_entMan.TryGetComponent<SpriteComponent>(organUid, out var drawSprite))
            organTint = drawSprite.Color;

        handle.DrawCircle(Size / 2f, organSide * 0.72f, new Color(0.07f, 0.08f, 0.10f, 0.55f));

        var time = (float) _timing.CurTime.TotalSeconds;
        var wobble = MathF.Sin(time * 1.7f) * 2f;

        var backOffset = _cameraOffset * BackDepthFactor + new Vector2(BackShift, BackShift + wobble);
        var backRect = UIBox2.FromDimensions(OrganCenter(backOffset) - new Vector2(organSide * BackGrow / 2f), new Vector2(organSide * BackGrow));
        DrawOrganTexture(handle, _organTexture, backRect, organTint * new Color(0.42f, 0.34f, 0.36f, 0.55f), _flipped);

        var frontRect = UIBox2.FromDimensions(OrganCenter(_cameraOffset) - new Vector2(organSide / 2f), new Vector2(organSide));
        DrawOrganTexture(handle, _organTexture, frontRect, organTint, _flipped);

        handle.DrawCircle(OrganCenter(_cameraOffset), organSide * 0.52f, new Color(0f, 0f, 0f, 0.25f), false);
    }

    private sealed class DecalOverlay : Control
    {
        private readonly OrganView _parent;
        private readonly List<OrganWound> _wounds = new();

        public event Action<Vector2>? OnMouseMoved;

        public DecalOverlay(OrganView parent)
        {
            _parent = parent;
        }

        public void SetWounds(List<OrganWound> wounds)
        {
            _wounds.Clear();
            _wounds.AddRange(wounds);
        }

        public OrganWound? HitTest(Vector2 pos)
        {
            OrganWound? best = null;
            var bestDist = float.MaxValue;
            var hitRadius = _parent.OrganSide * 0.028f;

            foreach (var wound in _wounds)
            {
                if (!_parent.IsWoundVisible(wound))
                    continue;

                var dist = (_parent.GetWoundScreenPos(wound) - pos).Length();
                if (dist > hitRadius || dist >= bestDist)
                    continue;

                bestDist = dist;
                best = wound;
            }

            return best;
        }

        protected override void MouseMove(GUIMouseMoveEventArgs args)
        {
            base.MouseMove(args);
            OnMouseMoved?.Invoke(args.RelativePosition);
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);

            foreach (var wound in _wounds)
            {
                if (!_parent.IsWoundVisible(wound))
                    continue;

                var pos = _parent.GetWoundScreenPos(wound);
                var tint = OrganView.GetWoundColor(wound);
                var organSide = _parent.OrganSide;

                if (_parent._decalTextures.Count > 0)
                {
                    var rng = new Random(wound.Id.GetHashCode() ^ 0x5f5f);
                    var tex = _parent._decalTextures[rng.Next(_parent._decalTextures.Count)];
                    var size = organSide * (0.035f + (float) rng.NextDouble() * 0.02f);
                    var rect = UIBox2.FromDimensions(pos - new Vector2(size / 2f), new Vector2(size, size));
                    handle.DrawTextureRect(tex, rect, tint);
                }
                else
                {
                    handle.DrawCircle(pos, organSide * 0.015f, tint);
                }
            }
        }
    }
}
