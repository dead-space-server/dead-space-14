// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.IO;
using System.Numerics;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Content.Client.DeadSpace.Surgery;

public sealed class SurgeryXrayClientSystem : EntitySystem
{
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SpriteSystem _sprites = default!;

    public SurgeryFilmMode Mode;

    private SixLabors.ImageSharp.Image<Rgba32>? _film;
    private EntityUid _filmBody;
    private int _filmKey;
    private readonly Dictionary<string, SurgeryArt?> _icons = new();
    private readonly Dictionary<string, SurgeryArt?> _protoIcons = new();
    private readonly Dictionary<string, (Rgba32[] Pixels, int Width, int Height)?> _organArt = new();

    public OwnedTexture? Sheet(EntityUid body)
    {
        if (!TryFilm(body, out var film, out _))
            return null;

        return _clyde.LoadTextureFromImage(film, "surgery-xray-film");
    }

    public byte[]? Film(EntityUid body, int width, int height, Vector2 pan, float zoom)
    {
        if (!TryFilm(body, out var film, out var scale) || zoom <= 0f)
            return null;

        width = Math.Clamp(width, 1, 480);
        height = Math.Clamp(height, 1, 480);
        var step = zoom / scale;
        var originX = width / 2f + pan.X;
        var originY = height / 2f + pan.Y;
        var left = (int) MathF.Floor((0.5f - (originX - 16f * zoom)) / step);
        var top = (int) MathF.Floor((0.5f - (originY - 16f * zoom)) / step);
        var right = (int) MathF.Ceiling((width - 0.5f - (originX - 16f * zoom)) / step);
        var bottom = (int) MathF.Ceiling((height - 0.5f - (originY - 16f * zoom)) / step);
        left = Math.Clamp(left, 0, film.Width - 1);
        top = Math.Clamp(top, 0, film.Height - 1);
        right = Math.Clamp(right, left + 1, film.Width);
        bottom = Math.Clamp(bottom, top + 1, film.Height);
        using var crop = film.Clone(ctx => ctx.Crop(new SixLabors.ImageSharp.Rectangle(left, top, right - left, bottom - top)));
        using var png = new MemoryStream();
        SixLabors.ImageSharp.ImageExtensions.SaveAsPng(crop, png);
        return png.ToArray();
    }

    private int Signature(EntityUid body)
    {
        var hash = 17;
        if (!TryComp<LimbBodyComponent>(body, out var limbs))
            return hash;

        foreach (var zone in limbs.Zones)
            hash = hash * 31 + (int) zone.Bone + (int) (zone.Bleed * 10f) + (zone.Embedded ? 3 : 0) + (int) zone.Flesh * 7
                + (zone.Severed ? 1009 : 0) + (zone.Atrophied ? 2003 : 0);

        hash = hash * 31 + (int) limbs.Present;

        foreach (var organ in limbs.Organs)
            hash = hash * 31 + (organ.Ruptured ? 1 : 0) + (organ.Missing ? 2 : 0);

        if (TryComp<SurgeryInfectionComponent>(body, out var infection))
        {
            foreach (var zone in infection.Rotting)
                hash = hash * 31 + (int) zone + 101;
        }

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

    private SurgeryArt? Icon(NetEntity item, string proto)
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

        SurgeryArt? loaded = null;
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

    private SurgeryArt? LoadIcon(ResPath path, int frameW, int frameH)
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
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            var art = SurgeryPng.Decode(bytes.ToArray(), frameW, frameH);
            _icons[key] = art;
            return art;
        }
    }

    private const string FilmRules = "Default";

    private bool TryFilm(EntityUid body, out SixLabors.ImageSharp.Image<Rgba32> film, out int scale)
    {
        scale = 1;
        film = null!;
        if (PickFilm(body) is not { } rules)
            return false;

        scale = Math.Max(1, rules.Scale);
        var stretch = TryComp<SpriteComponent>(body, out var sprite) ? sprite.Scale : Vector2.One;
        var key = Mix(Signature(body) * 31 + (int) (stretch.X * 100f) * 7 + (int) (stretch.Y * 100f) + (int) Mode * 977, rules.ID);
        if (_film != null && _filmBody == body && _filmKey == key)
        {
            film = _film;
            return true;
        }

        _film?.Dispose();
        _film = Render(body, rules, stretch);
        _filmBody = body;
        _filmKey = key;
        film = _film;
        return true;
    }

    private SurgeryFilmPrototype? PickFilm(EntityUid body)
    {
        TryComp<HumanoidAppearanceComponent>(body, out var look);
        SurgeryFilmPrototype? best = null;
        var bestScore = -1;
        foreach (var film in _prototypes.EnumeratePrototypes<SurgeryFilmPrototype>())
        {
            var score = 0;
            if (film.Species.Count > 0)
            {
                if (look == null || !film.Species.Contains(look.Species.Id))
                    continue;
                score += 2;
            }

            if (film.Sex is { } sex)
            {
                if (look == null || look.Sex != sex)
                    continue;
                score += 1;
            }

            if (score > bestScore || score == bestScore && film.ID == FilmRules)
            {
                best = film;
                bestScore = score;
            }
        }

        return best;
    }

    private SixLabors.ImageSharp.Image<Rgba32> Render(EntityUid body, SurgeryFilmPrototype rules, Vector2 stretch)
    {
        var paint = new SurgeryFilmPainter(rules.Scale, stretch);
        TryComp<LimbBodyComponent>(body, out var limbs);
        foreach (var shape in rules.Tissue)
        {
            if (!Severed(limbs, shape.Zone))
                Shape(paint, shape, shape.Color ?? rules.Tint, 0.16f, 0f);
        }

        foreach (var shape in rules.Bones)
        {
            if (shape.Kind == SurgeryFilmKind.Line && !Severed(limbs, shape.Zone))
                paint.Capsule(new Vector2(shape.Ax, shape.Ay), new Vector2(shape.Bx, shape.By), shape.W * 1.7f, rules.Tint, 0.1f);
        }

        var bones = Mode is SurgeryFilmMode.All or SurgeryFilmMode.Bones;
        var organs = Mode is SurgeryFilmMode.All or SurgeryFilmMode.Organs;
        var faint = bones ? 1f : 0.2f;
        var sprite = rules.Skeleton.Length > 0;
        if (sprite)
        {
            Skeleton(paint, rules, limbs, faint, false);
        }
        else
        {
            foreach (var shape in rules.Bones)
            {
                if (Severed(limbs, shape.Zone))
                    continue;

                var dim = Withered(limbs, shape.Zone);
                if (shape.Kind == SurgeryFilmKind.Hole)
                {
                    Shape(paint, shape, FilmBack, 0.92f * faint, 0f);
                    continue;
                }

                Shape(paint, shape, rules.Glow, (dim ? 0.12f : 0.28f) * faint, 0.45f);
                Shape(paint, shape, shape.Color ?? rules.Bone, (dim ? 0.45f : 0.96f) * faint, 0f);
            }
        }

        if (limbs != null)
        {
            if (organs)
            {
                Organs(paint, rules, limbs, body, false);
                if (bones && sprite)
                    Skeleton(paint, rules, limbs, 0.3f, true);
                foreach (var shape in rules.Bones)
                {
                    if (bones && !sprite && shape.Kind == SurgeryFilmKind.Line && !Severed(limbs, shape.Zone))
                        Shape(paint, shape, shape.Color ?? rules.Bone, 0.3f, 0f);
                }

                Organs(paint, rules, limbs, body, true);
            }

            Marks(paint, rules, limbs, body);
        }

        if (Mode is SurgeryFilmMode.All or SurgeryFilmMode.Foreign)
            Items(paint, rules, body);
        var pixels = paint.Pixels();
        return paint.ToImage(pixels);
    }

    private static readonly Color FilmBack = Color.FromHex("#07131f");

    private void Skeleton(SurgeryFilmPainter paint, SurgeryFilmPrototype rules, LimbBodyComponent? limbs, float alpha, bool trunkOnly)
    {
        foreach (var (zoneName, state) in rules.SkeletonStates)
        {
            if (!Enum.TryParse<LimbZone>(zoneName, out var zone) || Severed(limbs, zone))
                continue;

            if (trunkOnly && zone is not (LimbZone.Torso or LimbZone.Groin))
                continue;

            if (LoadIcon(new ResPath(rules.Skeleton) / $"{state}.png", 32, 32) is not { } art)
                continue;

            var dim = Withered(limbs, zone) ? 0.45f : 1f;
            paint.Blit(art.Pixels, art.Width, art.Height, new Vector2(16f, 16f), new Vector2(32f, 32f), false, alpha * dim);
        }
    }

    private static void Shape(SurgeryFilmPainter paint, SurgeryFilmShape shape, Color color, float alpha, float grow)
    {
        switch (shape.Kind)
        {
            case SurgeryFilmKind.Line:
                paint.Capsule(new Vector2(shape.Ax, shape.Ay), new Vector2(shape.Bx, shape.By), shape.W * 0.5f + grow, color, alpha);
                break;
            default:
                paint.Oval(new Vector2(shape.Ax, shape.Ay), new Vector2(shape.W * 0.5f + grow, shape.H * 0.5f + grow), color, alpha);
                break;
        }
    }

    private void Organs(SurgeryFilmPainter paint, SurgeryFilmPrototype rules, LimbBodyComponent limbs, EntityUid body, bool marks)
    {
        TryComp<HumanoidAppearanceComponent>(body, out var look);
        _prototypes.TryIndex<SurgerySpeciesPrototype>(look?.Species.Id ?? "", out var species);
        var shapes = new List<(SurgeryFilmShape Shape, LimbOrganState State, int Order)>();
        foreach (var organ in _prototypes.EnumeratePrototypes<SurgeryOrganPrototype>())
        {
            if (organ.Film.Count == 0
                || !Enum.TryParse<LimbOrgan>(organ.ID, out var kind)
                || (limbs.Present & (LimbOrganMask) (1 << (int) kind)) == 0)
                continue;

            var homeZone = species != null && species.Machine && organ.MachineZone.Length > 0 ? organ.MachineZone : organ.Zone;
            if (Enum.TryParse<LimbZone>(homeZone, out var home) && Severed(limbs, home))
                continue;

            var index = (int) kind;
            var state = (uint) index < (uint) limbs.Organs.Count ? limbs.Organs[index] : default;
            if (PairFilm(organ, species) && MergeFilm(organ.Film, species, organ.ID) is { } merged)
            {
                if (!Severed(limbs, merged.Zone))
                    shapes.Add((merged, state, shapes.Count));
                continue;
            }

            foreach (var shape in organ.Film)
            {
                if (!Severed(limbs, shape.Zone))
                    shapes.Add((RemapFilm(shape, species, organ.ID), state, shapes.Count));
            }
        }

        shapes.Sort((a, b) => a.Shape.Depth != b.Shape.Depth ? a.Shape.Depth.CompareTo(b.Shape.Depth) : a.Order.CompareTo(b.Order));
        foreach (var (shape, state, _) in shapes)
        {
            var center = new Vector2(shape.Ax, shape.Ay);
            var radii = new Vector2(shape.W * 0.5f, shape.H * 0.5f);
            var color = shape.Color ?? rules.Tint;
            if (rules.GrayOrgans)
            {
                var lum = 0.35f + (color.R * 0.30f + color.G * 0.59f + color.B * 0.11f) * 0.65f;
                color = new Color(lum * 0.92f, lum, lum * 1.04f);
            }

            var art = OrganArt(shape);
            if (!marks)
            {
                OrganBody(paint, art, center, radii, color, state.Missing || rules.GrayOrgans, state.Missing ? 0.3f : 0.95f, !state.Missing);
                continue;
            }

            if (state.Missing)
            {
                paint.Oval(center, radii, rules.Missing, 0.85f, 0.14f, true);
                var cross = radii * 0.55f;
                paint.Capsule(center - cross, center + cross, 0.09f, rules.Missing, 0.85f);
                paint.Capsule(center + new Vector2(-cross.X, cross.Y), center + new Vector2(cross.X, -cross.Y), 0.09f, rules.Missing, 0.85f);
                continue;
            }

            if (art == null)
                paint.Oval(center, radii, color, 0.9f, 0.14f);
            if (!state.Ruptured)
                continue;

            paint.Oval(center, radii + new Vector2(0.2f, 0.2f), rules.Blood, 0.55f, 0.2f);
            paint.Zigzag(center, new Vector2(1f, 0.6f), MathF.Max(radii.X, radii.Y) * 1.6f, 0.12f, rules.Crack, 0.95f);
        }
    }

    private static void OrganBody(SurgeryFilmPainter paint, (Rgba32[] Pixels, int Width, int Height)? art, Vector2 center, Vector2 radii, Color color, bool gray, float alpha, bool fill)
    {
        if (art is { } sprite)
        {
            paint.Blit(sprite.Pixels, sprite.Width, sprite.Height, center, radii * 2f, gray, alpha);
            return;
        }

        if (fill)
            paint.Oval(center, radii, color, 0.6f * alpha);
    }

    private static bool PairFilm(SurgeryOrganPrototype organ, SurgerySpeciesPrototype? species)
    {
        return organ.Pair
               && species != null
               && species.OrganSprite.Length > 0
               && species.OrganStates.ContainsKey(organ.ID);
    }

    private static SurgeryFilmShape RemapFilm(SurgeryFilmShape shape, SurgerySpeciesPrototype? species, string organ)
    {
        if (species == null || species.OrganSprite.Length == 0 || !species.OrganStates.TryGetValue(organ, out var state))
            return shape;

        return new SurgeryFilmShape
        {
            Kind = shape.Kind,
            Zone = shape.Zone,
            Ax = shape.Ax,
            Ay = shape.Ay,
            Bx = shape.Bx,
            By = shape.By,
            W = shape.W,
            H = shape.H,
            Color = shape.Color,
            Anchor = shape.Anchor,
            Depth = shape.Depth,
            Sprite = species.OrganSprite,
            State = state,
        };
    }

    private static SurgeryFilmShape? MergeFilm(List<SurgeryFilmShape> film, SurgerySpeciesPrototype? species, string organ)
    {
        if (species == null || !species.OrganStates.TryGetValue(organ, out var state) || film.Count == 0)
            return null;

        var ax = 0f;
        var ay = 0f;
        var width = 0f;
        var height = 0f;
        foreach (var shape in film)
        {
            ax += shape.Ax;
            ay += shape.Ay;
            width += shape.W;
            height = Math.Max(height, shape.H);
        }

        return new SurgeryFilmShape
        {
            Kind = SurgeryFilmKind.Oval,
            Zone = film[0].Zone,
            Ax = ax / film.Count,
            Ay = ay / film.Count,
            W = width * 0.95f,
            H = height,
            Color = film[0].Color,
            Depth = film[0].Depth,
            Sprite = species.OrganSprite,
            State = state,
        };
    }

    private (Rgba32[] Pixels, int Width, int Height)? OrganArt(SurgeryFilmShape shape)
    {
        if (shape.Sprite.Length == 0 || shape.State.Length == 0)
            return null;

        var key = shape.Sprite + "#" + shape.State;
        if (_organArt.TryGetValue(key, out var cached))
            return cached;

        (Rgba32[] Pixels, int Width, int Height)? art = null;
        if (LoadIcon(new ResPath(shape.Sprite) / $"{shape.State}.png", 32, 32) is { } image)
        {
            var source = image.Pixels;
            int left = image.Width, top = image.Height, right = -1, bottom = -1;
            for (var y = 0; y < image.Height; y++)
            {
                for (var x = 0; x < image.Width; x++)
                {
                    if (source[y * image.Width + x].A < 16)
                        continue;

                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                }
            }

            if (right >= left && bottom >= top)
            {
                var width = right - left + 1;
                var height = bottom - top + 1;
                var pixels = new Rgba32[width * height];
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                        pixels[y * width + x] = source[(top + y) * image.Width + left + x];
                }

                art = (pixels, width, height);
            }
        }

        _organArt[key] = art;
        return art;
    }

    private void Marks(SurgeryFilmPainter paint, SurgeryFilmPrototype rules, LimbBodyComponent limbs, EntityUid body)
    {
        var issues = new List<LimbScanIssue>();
        LimbScan.Build(limbs.Zones, limbs.Organs, out _, issues);
        foreach (var issue in issues)
        {
            if (issue.Organ)
                continue;

            if (!MarkShown(issue.Kind))
                continue;

            var zone = (LimbZone) issue.Index;
            var (at, along, width) = Anchor(rules, zone);
            var across = new Vector2(-along.Y, along.X);
            var crack = MathF.Max(1.1f, width * 2.6f);
            switch (issue.Kind)
            {
                case LimbIssueKind.ClosedFracture:
                case LimbIssueKind.RibClosed:
                    paint.Zigzag(at, across, crack, 0.11f, rules.Crack, 1f);
                    break;
                case LimbIssueKind.OpenFracture:
                case LimbIssueKind.RibOpen:
                    paint.Zigzag(at, across, crack * 1.2f, 0.14f, rules.Crack, 1f);
                    paint.Capsule(at + along * 0.2f, at + along * 0.8f + across * crack * 0.6f, 0.12f, rules.Bone, 0.95f);
                    paint.Oval(at + across * crack * 0.45f, new Vector2(0.7f, 0.55f), rules.Blood, 0.45f);
                    break;
                case LimbIssueKind.BoneStrain:
                case LimbIssueKind.RibCrack:
                case LimbIssueKind.Fracture:
                    paint.Zigzag(at, across, crack * 0.7f, 0.06f, rules.Crack, 0.9f, 2);
                    break;
                case LimbIssueKind.OpenWound:
                    paint.Capsule(at - along * 0.7f - across * width * 2.4f, at + along * 0.5f - across * width * 1.6f, 0.16f, FilmBack, 0.75f);
                    break;
                case LimbIssueKind.Bleed:
                    Bleed(paint, rules, at + across * width * 1.2f, limbs.Zones[issue.Index].Bleed);
                    break;
                case LimbIssueKind.Embedded:
                    var tip = at + across * width * 1.3f;
                    var end = tip + along * 1.4f + across * 0.5f;
                    paint.Capsule(tip, end, 0.5f, rules.Shard, 0.3f);
                    paint.Capsule(tip, end, 0.16f, rules.Shard, 1f);
                    paint.Capsule(tip, tip - along * 0.4f + across * 0.8f, 0.13f, rules.Shard, 1f);
                    break;
            }
        }

        for (var i = 0; i < limbs.Zones.Count && i < LimbWoundRules.BodyZoneCount; i++)
        {
            var zone = (LimbZone) i;
            if (!limbs.Zones[i].Severed || !_prototypes.TryIndex<SurgeryMarkPrototype>(zone.ToString(), out var mark))
                continue;

            var stump = mark.Cx != mark.Dx || mark.Cy != mark.Dy
                ? new Vector2((mark.Cx + mark.Dx) / 2f, (mark.Cy + mark.Dy) / 2f)
                : new Vector2(mark.Ax, mark.Ay);
            paint.Oval(stump, new Vector2(0.9f, 0.6f), FilmBack, 0.7f);
            paint.Oval(stump, new Vector2(0.9f, 0.6f), rules.Crack, 0.95f, 0.12f);
        }

        if (Mode is not (SurgeryFilmMode.All or SurgeryFilmMode.Organs) || !TryComp<SurgeryInfectionComponent>(body, out var infection))
            return;

        var foci = new HashSet<LimbZone>(infection.Rotting);
        foreach (var zone in foci)
        {
            var (at, _, width) = Anchor(rules, zone);
            var strong = infection.Rotting.Contains(zone) ? 0.55f : 0.35f;
            paint.Oval(at, new Vector2(1.5f + width, 1.2f + width), rules.Infection, strong * 0.45f);
            paint.Oval(at + new Vector2(0.5f, -0.4f), new Vector2(0.55f, 0.45f), rules.Infection, strong);
            paint.Oval(at + new Vector2(-0.6f, 0.5f), new Vector2(0.45f, 0.4f), rules.Infection, strong);
            paint.Oval(at, new Vector2(1.5f + width, 1.2f + width), rules.Infection, strong + 0.2f, 0.1f, true);
        }
    }

    private bool MarkShown(LimbIssueKind kind)
    {
        return kind switch
        {
            LimbIssueKind.ClosedFracture or LimbIssueKind.RibClosed or LimbIssueKind.OpenFracture or LimbIssueKind.RibOpen
                or LimbIssueKind.BoneStrain or LimbIssueKind.RibCrack or LimbIssueKind.Fracture
                => Mode is SurgeryFilmMode.All or SurgeryFilmMode.Bones,
            LimbIssueKind.OpenWound or LimbIssueKind.Bleed => Mode is SurgeryFilmMode.All or SurgeryFilmMode.Organs,
            LimbIssueKind.Embedded => Mode is SurgeryFilmMode.All or SurgeryFilmMode.Foreign,
            _ => true,
        };
    }

    private static void Bleed(SurgeryFilmPainter paint, SurgeryFilmPrototype rules, Vector2 at, float amount)
    {
        var size = 0.55f + MathF.Min(amount, 8f) * 0.12f;
        paint.Oval(at, new Vector2(size, size * 0.8f), rules.Blood, 0.55f);
        paint.Oval(at, new Vector2(size * 0.55f, size * 0.45f), rules.Blood, 0.8f);
        paint.Oval(at + new Vector2(size * 1.1f, size * 0.7f), new Vector2(0.22f, 0.3f), rules.Blood, 0.85f);
        paint.Oval(at + new Vector2(-size * 0.9f, size * 1.0f), new Vector2(0.18f, 0.25f), rules.Blood, 0.85f);
    }

    private (Vector2 At, Vector2 Along, float Width) Anchor(SurgeryFilmPrototype rules, LimbZone zone)
    {
        SurgeryFilmShape? pick = null;
        foreach (var shape in rules.Bones)
        {
            if (shape.Zone != zone || shape.Kind != SurgeryFilmKind.Line)
                continue;

            if (pick == null || shape.Anchor)
                pick = shape;
            if (shape.Anchor)
                break;
        }

        if (pick != null)
        {
            var a = new Vector2(pick.Ax, pick.Ay);
            var b = new Vector2(pick.Bx, pick.By);
            var along = b - a;
            return ((a + b) / 2f, along.LengthSquared() > 0.0001f ? Vector2.Normalize(along) : new Vector2(0f, 1f), pick.W * 0.5f);
        }

        if (_prototypes.TryIndex<SurgeryMarkPrototype>(zone.ToString(), out var mark))
            return (new Vector2(mark.Hx + mark.Hw / 2f, mark.Hy + mark.Hh / 2f), new Vector2(0f, 1f), 0.5f);

        return (new Vector2(16f, 16f), new Vector2(0f, 1f), 0.5f);
    }

    private void Items(SurgeryFilmPainter paint, SurgeryFilmPrototype rules, EntityUid body)
    {
        if (!TryComp<SurgeryCavityComponent>(body, out var hold))
            return;

        foreach (var spot in rules.Items)
        {
            var (item, proto) = spot.Zone switch
            {
                LimbZone.Head => (hold.HeadItem, hold.HeadProto),
                LimbZone.Groin => (hold.AbdomenItem, hold.AbdomenProto),
                LimbZone.Torso => (hold.ChestItem, hold.ChestProto),
                _ => (NetEntity.Invalid, ""),
            };
            if (Icon(item, proto) is not { } icon)
                continue;

            var center = new Vector2(spot.Ax, spot.Ay);
            paint.Oval(center, new Vector2(spot.W * 0.6f, spot.W * 0.6f), rules.Shard, 0.12f);
            paint.Blit(icon.Pixels, icon.Width, icon.Height, center, spot.W, true);
        }
    }

    private static bool Severed(LimbBodyComponent? limbs, LimbZone zone)
    {
        var index = LimbWoundRules.Index(zone);
        return limbs != null && LimbWoundRules.IsBody(zone) && (uint) index < (uint) limbs.Zones.Count && limbs.Zones[index].Severed;
    }

    private static bool Withered(LimbBodyComponent? limbs, LimbZone zone)
    {
        var index = LimbWoundRules.Index(zone);
        return limbs != null && LimbWoundRules.IsBody(zone) && (uint) index < (uint) limbs.Zones.Count && limbs.Zones[index].Atrophied;
    }
}

