// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.IO;
using System.Numerics;
using Content.Client.DeadSpace.Stylesheets;
using Content.Shared.DeadSpace.CCCCVars;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.Utility;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client.DeadSpace.UserInterface;

/// <summary>
/// Paints the "Dead Space" HUD theme in the player's projection colour (<see cref="CCCCVars.HologramColor"/>) and
/// window opacity (<see cref="CCCCVars.HologramOpacity"/>).
/// The theme's textures are drawn white and grey; every neutral pixel is multiplied by the colour, coloured pixels
/// (red crosses and the like) keep their colour. The painted copies live in memory under <see cref="TintedPath"/>,
/// where the theme's <c>uiTheme</c> points, and a colour change repaints the already loaded textures in place.
/// </summary>
public sealed partial class DeadSpaceHudTintUIController : UIController
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IResourceManager _resources = default!;
    [Dependency] private IResourceCache _cache = default!;

    public static readonly ResPath SourcePath = new("/Textures/_DeadSpace/Interface/Hud/DeadSpace");
    public static readonly ResPath TintedPath = new("/Textures/_DeadSpace/Interface/Hud/DeadSpaceTinted");

    public const string HudTheme = "DSDeadSpaceTheme";
    private const string DefaultHudTheme = "SS14DefaultTheme";

    /// <summary>Largest channel spread of a pixel that still counts as white/grey and takes the colour.</summary>
    private const int NeutralSpread = 12;

    /// <summary>Pixels darker than this are the slot body and follow the window opacity; brighter ones are the glow.</summary>
    private const float GlowLevel = 0.5f;

    private readonly MemoryContentRoot _root = new();
    private Color _color = DeadSpaceStylePalette.DefaultHologramColor;
    private float _opacity = 1f;
    private readonly List<ResPath> _files = [];

    public override void Initialize()
    {
        base.Initialize();

        foreach (var file in _resources.ContentFindFiles(SourcePath))
        {
            if (file.Extension == "png")
                _files.Add(file.RelativeTo(SourcePath));
        }

        _resources.AddRoot(TintedPath, _root);
        _cfg.OnValueChanged(CCCCVars.HologramOpacity, OnOpacityChanged);
        _opacity = OpacityFactor(_cfg.GetCVar(CCCCVars.HologramOpacity));
        _cfg.OnValueChanged(CCCCVars.HologramColor, OnColorChanged, true);
        _cfg.OnValueChanged(CCCCVars.InterfaceStyle, OnInterfaceStyleChanged);
    }

    /// <summary>
    /// The "Dead Space" window style brings its HUD along: switching to it moves a stock HUD to the matching one,
    /// switching away puts the stock HUD back. A HUD theme the player picked by hand is left alone.
    /// </summary>
    private void OnInterfaceStyleChanged(string style)
    {
        var hud = _cfg.GetCVar(CVars.InterfaceTheme);
        var deadSpace = string.Equals(style, CCCCVars.InterfaceStyleDeadSpace, StringComparison.OrdinalIgnoreCase);

        if (deadSpace && hud == DefaultHudTheme)
            _cfg.SetCVar(CVars.InterfaceTheme, HudTheme);
        else if (!deadSpace && hud == HudTheme)
            _cfg.SetCVar(CVars.InterfaceTheme, DefaultHudTheme);
    }

    private void OnColorChanged(string hex)
    {
        _color = Color.FromHex(hex, DeadSpaceStylePalette.DefaultHologramColor);
        Paint();
    }

    private void OnOpacityChanged(float opacity)
    {
        _opacity = OpacityFactor(opacity);
        Paint();
    }

    /// <summary>The window opacity setting, relative to its default, so the stock setting keeps the HUD as drawn.</summary>
    private static float OpacityFactor(float opacity)
    {
        return Math.Clamp(opacity / DeadSpaceStylePalette.DefaultHologramOpacity, 0f, 1f);
    }

    private void Paint()
    {
        var tint = new Vector3(_color.R, _color.G, _color.B);

        foreach (var file in _files)
        {
            try
            {
                using var stream = _resources.ContentFileRead(SourcePath / file);
                using var image = Image.Load<Rgba32>(stream);
                PaintPixels(image.GetPixelSpan(), tint, _opacity);

                using var output = new MemoryStream();
                image.SaveAsPng(output);
                _root.AddOrUpdateFile(file, output.ToArray());
            }
            catch (Exception e)
            {
                Log.Error($"Could not paint HUD texture {file}: {e.Message}");
                continue;
            }

            // Repaints textures the HUD already holds; ones not loaded yet will be read from the root as they are.
            _cache.ReloadResource<TextureResource>(TintedPath / file);
        }
    }

    private static void PaintPixels(Span<Rgba32> pixels, Vector3 tint, float opacity)
    {
        for (var i = 0; i < pixels.Length; i++)
        {
            var pixel = pixels[i];
            if (pixel.A == 0)
                continue;

            var max = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
            var min = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
            if (max - min > NeutralSpread)
                continue;

            var level = max / 255f;
            var alpha = level < GlowLevel ? (byte) Math.Round(pixel.A * opacity) : pixel.A;
            pixels[i] = new Rgba32(
                (byte) Math.Round(tint.X * level * 255f),
                (byte) Math.Round(tint.Y * level * 255f),
                (byte) Math.Round(tint.Z * level * 255f),
                alpha);
        }
    }
}
