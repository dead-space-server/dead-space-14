// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.Resources;
using Content.Shared.DeadSpace.PlanetSurvey;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;

namespace Content.Client.DeadSpace.PlanetSurvey;

public sealed class SurveyDebugOverlay : Overlay
{
    private const float LineHeight = 11f;
    private static readonly Color Normal = Color.FromHex("#ffe27a");
    private static readonly Color Alert = Color.FromHex("#ff6b6b");
    private static readonly Color Shadow = Color.Black.WithAlpha(0.8f);

    private readonly IEntityManager _entMan;
    private readonly IEyeManager _eye;
    private readonly SharedTransformSystem _xform;
    private readonly Font _font;

    public List<SurveyDebugEntry> Entries = new();

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public SurveyDebugOverlay(IEntityManager entMan, IEyeManager eye, SharedTransformSystem xform, IResourceCache cache)
    {
        _entMan = entMan;
        _eye = eye;
        _xform = xform;
        _font = cache.GetFont("/Fonts/NotoSans/NotoSans-Regular.ttf", 8);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.ScreenHandle;
        var mapId = args.MapId;

        foreach (var entry in Entries)
        {
            if (!_entMan.TryGetEntity(entry.Entity, out var uid) ||
                !_entMan.TryGetComponent<TransformComponent>(uid, out var xform) ||
                xform.MapID != mapId)
            {
                continue;
            }

            var screen = _eye.WorldToScreen(_xform.GetWorldPosition(xform));
            var lines = entry.Text.Split('\n');
            var top = screen + new Vector2(-40f, 18f);
            var color = entry.Alert ? Alert : Normal;

            for (var i = 0; i < lines.Length; i++)
            {
                var at = top + new Vector2(0f, i * LineHeight);
                handle.DrawString(_font, at + Vector2.One, lines[i], Shadow);
                handle.DrawString(_font, at, lines[i], color);
            }
        }
    }
}
