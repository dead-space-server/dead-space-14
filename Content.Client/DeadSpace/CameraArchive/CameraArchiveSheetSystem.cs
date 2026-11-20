// Dead Space 14, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.IO;
using System.Numerics;
using Content.Client.Paper.UI;
using Content.Client.RichText;
using Content.Client.Viewport;
using Content.Shared.DeadSpace.CameraArchives;
using Content.Shared.Paper;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.CameraArchives;

public sealed partial class CameraArchiveSheetSystem : EntitySystem
{
    private const string Marker = "{СЮДА КАРТИНКУ}";
    private static readonly Color Ink = new(25, 25, 25);

    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IRobustSerializer _serializer = default!;
    [Dependency] private CameraArchivePlaybackSystem _playback = default!;

    private readonly List<(EntityUid Paper, CameraArchivePrintComponent Shot, string Content)> _prints = new();
    private EntityUid _played;

    public override void FrameUpdate(float frameTime)
    {
        _prints.Clear();
        var query = EntityQueryEnumerator<CameraArchivePrintComponent, PaperComponent>();
        while (query.MoveNext(out var uid, out var shot, out var paper))
        {
            if ((shot.Still.Length > 0 || shot.Image.Length > 0) && paper.Content.Contains(Marker))
                _prints.Add((uid, shot, paper.Content));
        }

        if (_prints.Count == 0)
        {
            _playback.ClearSheet();
            _played = default;
            return;
        }

        var windows = new List<PaperWindow>();
        foreach (var child in _ui.WindowRoot.Children)
        {
            if (child is PaperWindow window)
                windows.Add(window);
        }

        var shown = false;
        var kept = false;
        foreach (var window in windows)
        {
            if (ArchiveStill(window))
            {
                if (kept)
                {
                    window.Close();
                    continue;
                }

                kept = true;
            }

            if (Mount(window))
                shown = true;
        }

        if (!shown)
        {
            _playback.ClearSheet();
            _played = default;
        }
    }

    private bool ArchiveStill(PaperWindow window)
    {
        var text = Rope.Collapse(window.Input.TextRope);
        if (!text.Contains(Marker))
            return false;

        foreach (var print in _prints)
        {
            if (print.Shot.Still.Length == 0)
                continue;

            if (text.Contains(print.Content) || print.Content.Contains(text))
                return true;
        }

        return false;
    }

    private bool Mount(PaperWindow window)
    {
        var text = Rope.Collapse(window.Input.TextRope);
        if (!text.Contains(Marker))
            return false;

        EntityUid paper = default;
        CameraArchivePrintComponent? shot = null;
        string? content = null;
        foreach (var print in _prints)
        {
            if (!text.Contains(print.Content) && !print.Content.Contains(text))
                continue;

            paper = print.Paper;
            shot = print.Shot;
            content = print.Content;
            break;
        }

        if (shot == null || content == null)
            return false;

        var written = FindWritten(window);
        if (written == null)
            return false;

        var at = content.IndexOf(Marker, StringComparison.Ordinal);
        if (at < 0)
            return false;

        var before = content[..at].TrimEnd();
        var after = content[(at + Marker.Length)..].TrimStart();
        var beforeMessage = new FormattedMessage();
        beforeMessage.AddMarkupPermissive(before);
        written.SetMessage(beforeMessage, UserFormattableTags.BaseAllowedTags, Ink);

        var parent = written.Parent;
        if (parent == null)
            return false;

        var place = written.GetPositionInParent();
        if (shot.Still.Length > 0)
        {
            ShowStill(paper, shot, parent, place + 1);
            var stillTail = Find<RichTextLabel>(parent, "CameraArchiveTail");
            if (stillTail == null)
            {
                stillTail = new RichTextLabel
                {
                    Name = "CameraArchiveTail",
                    VerticalAlignment = Control.VAlignment.Top,
                };
                stillTail.StyleClasses.Add("PaperWrittenText");
                parent.AddChild(stillTail);
            }

            var stillAfter = new FormattedMessage();
            stillAfter.AddMarkupPermissive(after);
            stillTail.SetMessage(stillAfter, UserFormattableTags.BaseAllowedTags, Ink);
            stillTail.SetPositionInParent(place + 2);
            return true;
        }

        var image = Find<TextureRect>(parent, "CameraArchiveShot");
        var tail = Find<RichTextLabel>(parent, "CameraArchiveTail");
        if (image == null)
        {
            image = new TextureRect
            {
                Name = "CameraArchiveShot",
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                MinSize = new Vector2(460, 260),
                Margin = new Thickness(0, 4),
                HorizontalAlignment = Control.HAlignment.Center,
            };
            parent.AddChild(image);
        }

        if (tail == null)
        {
            tail = new RichTextLabel
            {
                Name = "CameraArchiveTail",
                VerticalAlignment = Control.VAlignment.Top,
            };
            tail.StyleClasses.Add("PaperWrittenText");
            parent.AddChild(tail);
        }

        if (image.Texture == null)
        {
            try
            {
                using var stream = new MemoryStream(shot.Image);
                image.Texture = _clyde.LoadTextureFromPNGStream(stream, "camera-archive");
            }
            catch (Exception)
            {
                image.Texture = null;
            }
        }

        var afterMessage = new FormattedMessage();
        afterMessage.AddMarkupPermissive(after);
        tail.SetMessage(afterMessage, UserFormattableTags.BaseAllowedTags, Ink);

        var index = written.GetPositionInParent();
        image.SetPositionInParent(index + 1);
        tail.SetPositionInParent(index + 2);
        return false;
    }

    private void ShowStill(EntityUid paper, CameraArchivePrintComponent shot, Control parent, int at)
    {
        var view = Find<ScalingViewport>(parent, "CameraArchiveView");
        if (view == null)
        {
            view = new ScalingViewport
            {
                Name = "CameraArchiveView",
                MinSize = new Vector2(460, 260),
                Margin = new Thickness(0, 4),
                HorizontalAlignment = Control.HAlignment.Center,
                MouseFilter = Control.MouseFilterMode.Ignore,
            };
            view.MinSize = new Vector2(460, 345);
            view.ViewportSize = new Vector2i(480, 360);
            parent.AddChild(view);
        }

        view.SetPositionInParent(at);
        if (_played == paper)
            return;

        try
        {
            using var stream = new MemoryStream(shot.Still);
            _serializer.DeserializeDirect<CameraArchive>(stream, out var still);
            var eye = _playback.PlaySheet(still).Eye;
            var step = Math.Clamp(shot.Magnification, 1f, 4f);
            eye.Zoom = CameraArchiveViewMath.EyeZoom(still.Zoom, step);
            eye.Offset = shot.Pan;
            view.Eye = eye;
            _played = paper;
        }
        catch (Exception)
        {
            view.Eye = null;
            _played = paper;
        }
    }

    private static RichTextLabel? FindWritten(Control root)
    {
        foreach (var child in root.Children)
        {
            if (child is RichTextLabel label
                && label.Visible
                && label.StyleClasses.Contains("PaperWrittenText"))
                return label;

            var nested = FindWritten(child);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static T? Find<T>(Control parent, string name) where T : Control
    {
        foreach (var child in parent.Children)
        {
            if (child.Name == name && child is T typed)
                return typed;
        }

        return null;
    }
}
