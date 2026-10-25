// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.IO;
using System.Numerics;
using Content.Shared.Buckle.Components;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Standing;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Content.Client.DeadSpace.Surgery;

public sealed class SurgeryXrayClientSystem : EntitySystem
{
    private const int Lens = 32;
    private const int Crop = 8;
    private static readonly Rgba32 LensFill = new(13, 15, 18, 230);

    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SpriteSystem _sprites = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private SixLabors.ImageSharp.Image<Rgba32>? _bones;
    private string? _bonesPath;
    private ICursor? _lens;
    private (int X, int Y, int Rot, int Mark) _lensKey;
    private bool _lensReady;
    private Control? _host;
    private ICursor? _hostCursor;
    private bool _worldCursor;
    private readonly Dictionary<string, SixLabors.ImageSharp.Image<Rgba32>?> _icons = new();
    private readonly Dictionary<string, SixLabors.ImageSharp.Image<Rgba32>?> _protoIcons = new();

    public override void FrameUpdate(float frameTime)
    {
        if (_players.LocalEntity is not { } user
            || !_hands.TryGetActiveItem(user, out var held)
            || !TryComp<SurgeryXrayComponent>(held, out var scan)
            || !scan.On)
        {
            Clear();
            return;
        }

        var mouse = _eye.PixelToMap(_input.MouseScreenPosition);
        if (!Under(mouse, scan, out var body, out var pixel, out var rotation))
        {
            Clear();
            return;
        }

        if (!FaceOn(body))
        {
            ClearLens();
            return;
        }

        if (!TryBones(scan, out var sheet))
        {
            ClearLens();
            return;
        }

        var key = ((int) pixel.X, (int) pixel.Y, (int) Math.Round(rotation.Degrees), Signature(body));
        if (!_lensReady || key != _lensKey)
        {
            using var image = Paint(sheet, body, pixel, rotation);
            var next = _clyde.CreateCursor(image, new Vector2i(Lens / 2, Lens / 2));
            var previous = _lens;
            _lens = next;
            _lensKey = key;
            _lensReady = true;
            Stick(next);
            previous?.Dispose();
        }
        else if (_lens != null)
        {
            Stick(_lens);
        }
    }

    private bool FaceOn(EntityUid body)
    {
        if (TryComp<BuckleComponent>(body, out var buckle) && buckle.Buckled
            && (!TryComp<StandingStateComponent>(body, out var seat) || seat.Standing))
            return false;

        if (TryComp<StandingStateComponent>(body, out var standing) && !standing.Standing)
            return true;

        if (_eye.CurrentEye == null)
            return true;

        var dir = (_transform.GetWorldRotation(body) + _eye.CurrentEye.Rotation).GetCardinalDir();
        return dir is Direction.South or Direction.North;
    }

    private bool Under(MapCoordinates mouse, SurgeryXrayComponent scan, out EntityUid body, out Vector2 pixel, out Angle rotation)
    {
        body = default;
        pixel = default;
        rotation = Angle.Zero;
        if (mouse.MapId == MapId.Nullspace)
            return false;

        var best = 0.45f * 0.45f;
        var found = false;
        var query = EntityQueryEnumerator<HumanoidAppearanceComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var look, out var sprite, out var xform))
        {
            if (xform.MapID != mouse.MapId || Boneless(scan, look.Species.Id))
                continue;

            var origin = _transform.GetWorldPosition(uid) + sprite.Rotation.RotateVec(sprite.Offset);
            var dist = (origin - mouse.Position).LengthSquared();
            if (dist > best)
                continue;

            var local = (-sprite.Rotation).RotateVec(mouse.Position - origin);
            best = dist;
            body = uid;
            pixel = new Vector2(local.X * 32f + 16f, 16f - local.Y * 32f);
            rotation = sprite.Rotation;
            found = true;
        }

        return found;
    }

    private static bool Boneless(SurgeryXrayComponent scan, string species)
    {
        foreach (var name in scan.Boneless)
        {
            if (name.Equals(species, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private bool TryBones(SurgeryXrayComponent scan, out SixLabors.ImageSharp.Image<Rgba32> sheet)
    {
        var path = $"{scan.Sprite}/{scan.State}.png";
        if (_bones != null && _bonesPath == path)
        {
            sheet = _bones;
            return true;
        }

        if (!_cache.TryContentFileRead(path, out var stream))
        {
            sheet = null!;
            return false;
        }

        using (stream)
            _bones = SixLabors.ImageSharp.Image.Load<Rgba32>(stream);

        _bonesPath = path;
        sheet = _bones;
        return true;
    }

    private SixLabors.ImageSharp.Image<Rgba32> Paint(SixLabors.ImageSharp.Image<Rgba32> sheet, EntityUid body, Vector2 pixel, Angle rotation)
    {
        var half = Crop / 2f;
        var left = (int) Math.Clamp(pixel.X - half, 0f, Math.Max(0f, sheet.Width - Crop));
        var top = (int) Math.Clamp(pixel.Y - half, 0f, Math.Max(0f, sheet.Height - Crop));
        using var marked = sheet.Clone();
        Stamp(marked, body);
        using var crop = marked.Clone(ctx => ctx.Crop(new SixLabors.ImageSharp.Rectangle(left, top, Crop, Crop)));
        using var stream = new MemoryStream();
        SixLabors.ImageSharp.ImageExtensions.SaveAsBmp(crop, stream);
        var samples = ReadBgra(stream.ToArray(), Crop, Crop);

        var image = new SixLabors.ImageSharp.Image<Rgba32>(Lens, Lens, LensFill);
        var center = new Vector2((Lens - 1) / 2f, (Lens - 1) / 2f);
        var turn = Matrix3x2.CreateRotation((float) rotation.Theta, center);
        for (var y = 0; y < Lens; y++)
        {
            for (var x = 0; x < Lens; x++)
            {
                var unrot = Vector2.Transform(new Vector2(x, y), turn);
                var sx = (int) (unrot.X / Lens * Crop);
                var sy = (int) (unrot.Y / Lens * Crop);
                if (sx < 0 || sy < 0 || sx >= Crop || sy >= Crop)
                    continue;

                var sample = samples[sy * Crop + sx];
                if (sample.A == 0)
                    continue;

                image[x, y] = sample;
            }
        }

        return image;
    }

    public OwnedTexture? Sheet(EntityUid body)
    {
        if (!TryBones(new SurgeryXrayComponent(), out var sheet))
            return null;

        using var marked = sheet.Clone();
        Stamp(marked, body);
        return _clyde.LoadTextureFromImage(marked, "surgery-xray-film");
    }

    public byte[]? Film(EntityUid body, int width, int height, Vector2 pan, float zoom)
    {
        if (!TryBones(new SurgeryXrayComponent(), out var sheet) || zoom <= 0f)
            return null;

        width = Math.Clamp(width, 1, 480);
        height = Math.Clamp(height, 1, 480);
        using var marked = sheet.Clone();
        Stamp(marked, body);
        var originX = width / 2f + pan.X;
        var originY = height / 2f + pan.Y;
        var left = (int) MathF.Floor((0.5f - (originX - 16f * zoom)) / zoom);
        var top = (int) MathF.Floor((0.5f - (originY - 16f * zoom)) / zoom);
        var right = (int) MathF.Ceiling((width - 0.5f - (originX - 16f * zoom)) / zoom);
        var bottom = (int) MathF.Ceiling((height - 0.5f - (originY - 16f * zoom)) / zoom);
        left = Math.Clamp(left, 0, marked.Width - 1);
        top = Math.Clamp(top, 0, marked.Height - 1);
        right = Math.Clamp(right, left + 1, marked.Width);
        bottom = Math.Clamp(bottom, top + 1, marked.Height);
        using var crop = marked.Clone(ctx => ctx.Crop(new SixLabors.ImageSharp.Rectangle(left, top, right - left, bottom - top)));
        using var png = new MemoryStream();
        SixLabors.ImageSharp.ImageExtensions.SaveAsPng(crop, png);
        return png.ToArray();
    }

    private static Rgba32[] ReadBgra(byte[] bytes, int width, int height, bool keepBlack = false)
    {
        var pixels = new Rgba32[width * height];
        if (bytes.Length < 54)
            return pixels;

        var offset = BitConverter.ToInt32(bytes, 10);
        var stored = BitConverter.ToInt32(bytes, 22);
        var bpp = BitConverter.ToInt16(bytes, 28);
        if ((bpp != 24 && bpp != 32) || offset < 0)
            return pixels;

        var topDown = stored < 0;
        var rows = Math.Abs(stored);
        var channels = bpp / 8;
        var stride = (width * bpp + 31) / 32 * 4;
        for (var y = 0; y < height && y < rows; y++)
        {
            var srcY = topDown ? y : rows - 1 - y;
            var row = offset + srcY * stride;
            for (var x = 0; x < width; x++)
            {
                var i = row + x * channels;
                if ((uint) i >= (uint) bytes.Length || i + channels > bytes.Length)
                    continue;

                var b = bytes[i];
                var g = bytes[i + 1];
                var r = bytes[i + 2];
                var a = channels == 4 ? bytes[i + 3] : (byte) 255;
                if (a == 0 || (!keepBlack && (r | g | b) == 0))
                    continue;

                pixels[y * width + x] = new Rgba32(r, g, b, a);
            }
        }

        return pixels;
    }

    private int Signature(EntityUid body)
    {
        var hash = 17;
        if (!TryComp<LimbBodyComponent>(body, out var limbs))
            return hash;

        foreach (var zone in limbs.Zones)
            hash = hash * 31 + (int) zone.Bone + (int) (zone.Bleed * 10f) + (zone.Embedded ? 3 : 0) + (int) zone.Flesh;

        foreach (var organ in limbs.Organs)
            hash = hash * 31 + (organ.Ruptured ? 1 : 0) + (organ.Missing ? 2 : 0);

        if (TryComp<SurgeryCavityComponent>(body, out var hold))
        {
            hash = hash * 31 + hold.HeadItem.Id;
            hash = hash * 31 + hold.AbdomenItem.Id * 5;
            hash = hash * 31 + hold.ChestItem.Id * 11;
            hash = Mix(hash, hold.HeadProto);
            hash = Mix(hash, hold.AbdomenProto);
            hash = Mix(hash, hold.ChestProto);
        }
        return hash;
    }

    private static int Mix(int hash, string text)
    {
        foreach (var letter in text)
            hash = hash * 31 + letter;

        return hash;
    }

    private void Stamp(SixLabors.ImageSharp.Image<Rgba32> sheet, EntityUid body)
    {
        if (!TryComp<LimbBodyComponent>(body, out var limbs))
            return;

        BehindOrgans(sheet, limbs);
        var issues = new List<LimbScanIssue>();
        LimbScan.Build(limbs.Zones, limbs.Organs, out _, issues);
        foreach (var issue in issues)
        {
            if (issue.Organ)
            {
                if (issue.Kind == LimbIssueKind.Rupture)
                    TearOrgan(sheet, (LimbOrgan) issue.Index);
                continue;
            }

            var zone = (LimbZone) issue.Index;
            var spot = BoneAt(zone);
            switch (issue.Kind)
            {
                case LimbIssueKind.ClosedFracture:
                case LimbIssueKind.RibClosed:
                    Crack(sheet, spot.X, spot.Y, false);
                    break;
                case LimbIssueKind.OpenFracture:
                case LimbIssueKind.RibOpen:
                    Crack(sheet, spot.X, spot.Y, true);
                    break;
                case LimbIssueKind.BoneStrain:
                case LimbIssueKind.RibCrack:
                    Nick(sheet, spot.X, spot.Y);
                    break;
                case LimbIssueKind.Bleed:
                    Bleed(sheet, zone == LimbZone.Groin ? spot.X + 1 : spot.X, zone == LimbZone.Groin ? spot.Y : spot.Y);
                    break;
                case LimbIssueKind.Rupture:
                    Tear(sheet, spot.X, spot.Y);
                    break;
                case LimbIssueKind.Embedded:
                    Splinter(sheet, spot.X - 2, spot.Y);
                    break;
            }
        }

        HoldMark(sheet, body, hold => (hold.ChestItem, hold.ChestProto), 16, 14, 5);
        HoldMark(sheet, body, hold => (hold.AbdomenItem, hold.AbdomenProto), 16, 20, 6);
    }

    private void HoldMark(
        SixLabors.ImageSharp.Image<Rgba32> sheet,
        EntityUid body,
        System.Func<SurgeryCavityComponent, (NetEntity Item, string Proto)> pick,
        int x,
        int y,
        int span = 4)
    {
        if (!TryComp<SurgeryCavityComponent>(body, out var hold))
            return;

        var (item, proto) = pick(hold);
        var icon = Icon(item, proto);
        if (icon == null)
            return;

        BlitGray(sheet, icon, x, y, span);
    }

    private SixLabors.ImageSharp.Image<Rgba32>? Icon(NetEntity item, string proto)
    {
        if (TryGetEntity(item, out var uid))
        {
            var live = LivePath(uid.Value);
            if (live != null)
                return LoadIcon(live.Value.Path, live.Value.Width, live.Value.Height);
        }

        if (proto.Length == 0)
            return null;

        if (_protoIcons.TryGetValue(proto, out var cached))
            return cached;

        SixLabors.ImageSharp.Image<Rgba32>? loaded = null;
        if (_prototypes.TryIndex<EntityPrototype>(proto, out var prototype))
        {
            foreach (var provider in _sprites.GetPrototypeTextures(prototype, out _))
            {
                if (provider is not RSI.State state || state.StateId.Name is not { } name)
                    continue;

                loaded = LoadIcon(state.RSI.Path / $"{name}.png", state.RSI.Size.X, state.RSI.Size.Y);
                break;
            }
        }

        _protoIcons[proto] = loaded;
        return loaded;
    }

    private (ResPath Path, int Width, int Height)? LivePath(EntityUid uid)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return null;

        foreach (var layer in sprite.AllLayers)
        {
            if (!layer.Visible || !layer.RsiState.IsValid || layer.RsiState.Name is not { } name)
                continue;

            var rsi = layer.ActualRsi ?? layer.Rsi ?? sprite.BaseRSI;
            if (rsi == null)
                continue;

            return (rsi.Path / $"{name}.png", rsi.Size.X, rsi.Size.Y);
        }

        return null;
    }

    private SixLabors.ImageSharp.Image<Rgba32>? LoadIcon(ResPath path, int frameW, int frameH)
    {
        var key = path.ToString();
        if (_icons.TryGetValue(key, out var cached))
            return cached;

        if (!_cache.TryContentFileRead(path, out var stream))
        {
            _icons[key] = null;
            return null;
        }

        using (stream)
        {
            var full = SixLabors.ImageSharp.Image.Load<Rgba32>(stream);
            if (frameW > 0 && frameH > 0 && (full.Width != frameW || full.Height != frameH))
            {
                var cropped = full.Clone(ctx => ctx.Crop(new SixLabors.ImageSharp.Rectangle(0, 0, Math.Min(frameW, full.Width), Math.Min(frameH, full.Height))));
                full.Dispose();
                full = cropped;
            }

            _icons[key] = full;
            return full;
        }
    }

    private static void BlitGray(SixLabors.ImageSharp.Image<Rgba32> sheet, SixLabors.ImageSharp.Image<Rgba32> icon, int x, int y, int span)
    {
        using var stream = new MemoryStream();
        SixLabors.ImageSharp.ImageExtensions.SaveAsBmp(icon, stream);
        var samples = ReadBgra(stream.ToArray(), icon.Width, icon.Height, false);
        var left = x - span / 2;
        var top = y - span / 2;
        for (var j = 0; j < span; j++)
        {
            for (var i = 0; i < span; i++)
            {
                var sx = i * icon.Width / span;
                var sy = j * icon.Height / span;
                if ((uint) sx >= (uint) icon.Width || (uint) sy >= (uint) icon.Height)
                    continue;

                var sample = samples[sy * icon.Width + sx];
                if (sample.A < 16 || sample.R > 235 && sample.G > 235 && sample.B > 235)
                    continue;

                var lum = (byte) ((sample.R * 30 + sample.G * 59 + sample.B * 11) / 100);
                Put(sheet, left + i, top + j, new Rgba32(lum, lum, lum, sample.A));
            }
        }
    }

    private readonly record struct OrganMark(LimbOrgan Organ, string State, int Y, int X0, int X1);

    private static readonly OrganMark[] OrganMarks =
    {
        new(LimbOrgan.Lungs, "lung-l", 12, 11, 14),
        new(LimbOrgan.Lungs, "lung-l", 14, 10, 14),
        new(LimbOrgan.Lungs, "lung-l", 16, 10, 14),
        new(LimbOrgan.Lungs, "lung-r", 12, 19, 21),
        new(LimbOrgan.Lungs, "lung-r", 14, 19, 22),
        new(LimbOrgan.Lungs, "lung-r", 16, 19, 22),
        new(LimbOrgan.Heart, "heart-off", 12, 15, 15),
        new(LimbOrgan.Heart, "heart-off", 14, 15, 15),
        new(LimbOrgan.Heart, "heart-off", 16, 15, 15),
        new(LimbOrgan.Heart, "heart-off", 12, 18, 18),
        new(LimbOrgan.Heart, "heart-off", 14, 18, 18),
        new(LimbOrgan.Heart, "heart-off", 16, 18, 18),
        new(LimbOrgan.Liver, "liver", 18, 10, 14),
        new(LimbOrgan.Liver, "liver", 19, 11, 14),
        new(LimbOrgan.Stomach, "stomach", 18, 19, 22),
        new(LimbOrgan.Stomach, "stomach", 19, 19, 22),
        new(LimbOrgan.Kidneys, "kidney-l", 20, 12, 14),
        new(LimbOrgan.Kidneys, "kidney-r", 20, 19, 21),
    };

    private void BehindOrgans(SixLabors.ImageSharp.Image<Rgba32> sheet, LimbBodyComponent limbs)
    {
        using var bones = new MemoryStream();
        SixLabors.ImageSharp.ImageExtensions.SaveAsBmp(sheet, bones);
        var bone = ReadBgra(bones.ToArray(), sheet.Width, sheet.Height, false);
        var ink = new Dictionary<string, Rgba32>();

        foreach (var mark in OrganMarks)
        {
            if (Gone(limbs, mark.Organ))
                continue;

            if (!ink.TryGetValue(mark.State, out var color))
            {
                color = Tone(Average(OrganIcon(mark.State)), mark.State);
                ink[mark.State] = color;
            }

            for (var x = mark.X0; x <= mark.X1; x++)
                Put(sheet, x, mark.Y, color);
        }

        for (var y = 0; y < sheet.Height; y++)
        {
            for (var x = 0; x < sheet.Width; x++)
            {
                var pixel = bone[y * sheet.Width + x];
                if (pixel.A < 16)
                    continue;

                sheet[x, y] = pixel;
            }
        }
    }

    private static Rgba32 Average(SixLabors.ImageSharp.Image<Rgba32>? icon)
    {
        if (icon == null)
            return new Rgba32(80, 80, 80, 255);

        using var stream = new MemoryStream();
        SixLabors.ImageSharp.ImageExtensions.SaveAsBmp(icon, stream);
        var samples = ReadBgra(stream.ToArray(), icon.Width, icon.Height, false);
        var red = 0;
        var green = 0;
        var blue = 0;
        var count = 0;
        for (var i = 0; i < samples.Length; i++)
        {
            if (samples[i].A < 16)
                continue;

            red += samples[i].R;
            green += samples[i].G;
            blue += samples[i].B;
            count++;
        }

        if (count == 0)
            return new Rgba32(80, 80, 80, 255);

        return new Rgba32((byte) (red / count), (byte) (green / count), (byte) (blue / count), 255);
    }

    private static Rgba32 Tone(Rgba32 color, string state)
    {
        var target = state switch
        {
            "lung-l" or "lung-r" => new Rgba32(255, 214, 204, 255),
            "heart-off" => new Rgba32(120, 20, 64, 255),
            "liver" => new Rgba32(96, 32, 26, 255),
            "stomach" => new Rgba32(232, 150, 72, 255),
            "kidney-l" or "kidney-r" => new Rgba32(110, 40, 96, 255),
            _ => color,
        };
        var amount = state switch
        {
            "lung-l" or "lung-r" => 0.55f,
            "heart-off" => 0.45f,
            "liver" => 0.5f,
            "stomach" => 0.5f,
            "kidney-l" or "kidney-r" => 0.55f,
            _ => 0f,
        };
        var keep = 1f - amount;
        return new Rgba32(
            (byte) (color.R * keep + target.R * amount),
            (byte) (color.G * keep + target.G * amount),
            (byte) (color.B * keep + target.B * amount),
            255);
    }

    private SixLabors.ImageSharp.Image<Rgba32>? OrganIcon(string state)
    {
        return LoadIcon(new ResPath($"/Textures/Mobs/Species/Human/organs.rsi/{state}.png"), 32, 32);
    }

    private static bool Gone(LimbBodyComponent limbs, LimbOrgan organ)
    {
        var bit = (LimbOrganMask) (1 << (int) organ);
        if ((limbs.Present & bit) == 0)
            return true;

        var index = (int) organ;
        return (uint) index < (uint) limbs.Organs.Count && limbs.Organs[index].Missing;
    }

    private static (int X, int Y) BoneAt(LimbZone zone)
    {
        return zone switch
        {
            LimbZone.Head => (16, 6),
            LimbZone.Torso => (16, 15),
            LimbZone.Groin => (16, 22),
            LimbZone.ArmLeft => (9, 16),
            LimbZone.ArmRight => (23, 16),
            LimbZone.LegLeft => (19, 25),
            LimbZone.LegRight => (13, 25),
            LimbZone.HandLeft => (6, 20),
            LimbZone.HandRight => (26, 20),
            LimbZone.FootLeft => (20, 31),
            LimbZone.FootRight => (12, 31),
            _ => (16, 16),
        };
    }

    private static void TearOrgan(SixLabors.ImageSharp.Image<Rgba32> sheet, LimbOrgan organ)
    {
        var spot = organ switch
        {
            LimbOrgan.Brain => (16, 4),
            LimbOrgan.Eyes => (15, 5),
            LimbOrgan.Heart => (17, 14),
            LimbOrgan.Lungs => (13, 14),
            LimbOrgan.Stomach => (16, 17),
            LimbOrgan.Liver => (19, 16),
            LimbOrgan.Kidneys => (15, 21),
            LimbOrgan.Intestines => (17, 23),
            _ => (16, 15),
        };
        Tear(sheet, spot.Item1, spot.Item2);
    }

    private static readonly Rgba32 BoneInk = new(232, 228, 214, 255);
    private static readonly Rgba32 BloodInk = new(120, 22, 30, 220);
    private static readonly Rgba32 Gap = new(0, 0, 0, 0);

    private static void Crack(SixLabors.ImageSharp.Image<Rgba32> sheet, int x, int y, bool open)
    {
        Put(sheet, x, y, Gap);
        Put(sheet, x, y + 1, Gap);
        Put(sheet, x - 1, y, BoneInk);
        Put(sheet, x + 1, y + 1, BoneInk);
        if (!open)
            return;

        Put(sheet, x + 1, y, Gap);
        Put(sheet, x - 1, y + 1, Gap);
        Put(sheet, x - 2, y - 1, BoneInk);
        Put(sheet, x + 2, y + 2, BoneInk);
    }

    private static void Nick(SixLabors.ImageSharp.Image<Rgba32> sheet, int x, int y)
    {
        Put(sheet, x, y, Gap);
        Put(sheet, x + 1, y, BoneInk);
    }

    private void Bleed(SixLabors.ImageSharp.Image<Rgba32> sheet, int x, int y)
    {
        var icon = LoadIcon(new ResPath("/Textures/Mobs/Effects/brute_damage.rsi/Chest_Brute_30.png"), 32, 32);
        if (icon == null)
        {
            Blot(sheet, x, y);
            return;
        }

        BlitKeep(sheet, icon, x, y, 3);
    }

    private static void BlitKeep(SixLabors.ImageSharp.Image<Rgba32> sheet, SixLabors.ImageSharp.Image<Rgba32> icon, int x, int y, int span)
    {
        using var stream = new MemoryStream();
        SixLabors.ImageSharp.ImageExtensions.SaveAsBmp(icon, stream);
        var samples = ReadBgra(stream.ToArray(), icon.Width, icon.Height, false);
        var left = x - span / 2;
        var top = y - span / 2;
        for (var j = 0; j < span; j++)
        {
            for (var i = 0; i < span; i++)
            {
                var sx = i * icon.Width / span;
                var sy = j * icon.Height / span;
                if ((uint) sx >= (uint) icon.Width || (uint) sy >= (uint) icon.Height)
                    continue;

                var sample = samples[sy * icon.Width + sx];
                if (sample.A < 16)
                    continue;

                Put(sheet, left + i, top + j, sample);
            }
        }
    }

    private static void Blot(SixLabors.ImageSharp.Image<Rgba32> sheet, int x, int y)
    {
        Put(sheet, x, y, BloodInk);
        Put(sheet, x + 1, y, BloodInk);
        Put(sheet, x, y + 1, new Rgba32(70, 10, 16, 200));
        Put(sheet, x + 1, y + 1, BloodInk);
    }

    private static void Tear(SixLabors.ImageSharp.Image<Rgba32> sheet, int x, int y)
    {
        Put(sheet, x, y, Gap);
        Put(sheet, x + 1, y, BoneInk);
        Put(sheet, x + 2, y, Gap);
        Put(sheet, x + 1, y + 1, Gap);
        Put(sheet, x, y + 1, BoneInk);
        Put(sheet, x + 2, y + 1, BoneInk);
    }

    private static void Splinter(SixLabors.ImageSharp.Image<Rgba32> sheet, int x, int y)
    {
        Put(sheet, x, y, BoneInk);
        Put(sheet, x + 1, y + 1, BoneInk);
        Put(sheet, x + 2, y, BoneInk);
    }

    private static void Put(SixLabors.ImageSharp.Image<Rgba32> sheet, int x, int y, Rgba32 color)
    {
        if (x < 0 || y < 0 || x >= sheet.Width || y >= sheet.Height)
            return;

        sheet[x, y] = color;
    }

    private void Stick(ICursor cursor)
    {
        var hovered = _ui.CurrentlyHovered;
        if (hovered == null)
        {
            ReleaseHost();
            _ui.WorldCursor = cursor;
            _worldCursor = true;
            return;
        }

        if (_worldCursor)
        {
            _ui.WorldCursor = null;
            _worldCursor = false;
        }

        if (_host != hovered)
        {
            ReleaseHost();
            _host = hovered;
            _hostCursor = hovered.CustomCursorShape;
        }

        if (hovered.CustomCursorShape != cursor)
            hovered.CustomCursorShape = cursor;
    }

    private void Clear()
    {
        ClearLens();
    }

    private void ClearLens()
    {
        ReleaseHost();
        if (_worldCursor)
        {
            _ui.WorldCursor = null;
            _worldCursor = false;
        }

        _lens?.Dispose();
        _lens = null;
        _lensReady = false;
    }

    private void ReleaseHost()
    {
        if (_host == null)
            return;

        _host.CustomCursorShape = _hostCursor;
        _host = null;
        _hostCursor = null;
    }
}
