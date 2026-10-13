// Dead Space 14, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.IO;
using Content.Client.Paper.UI;
using Content.Client.RichText;
using Content.Shared.DeadSpace.CameraArchives;
using Content.Shared.Paper;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.CameraArchives;

public sealed class CameraArchiveSheetSystem : EntitySystem
{
    private const string Marker = "{СЮДА КАРТИНКУ}";
    private static readonly Color Ink = new(25, 25, 25);

    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IClyde _clyde = default!;

    private readonly List<(CameraArchivePrintComponent Shot, string Content)> _prints = new();

    public override void FrameUpdate(float frameTime)
    {
        _prints.Clear();
        var query = EntityQueryEnumerator<CameraArchivePrintComponent, PaperComponent>();
        while (query.MoveNext(out _, out var shot, out var paper))
        {
            if (shot.Image.Length > 0 && paper.Content.Contains(Marker))
                _prints.Add((shot, paper.Content));
        }

        if (_prints.Count == 0)
            return;

        foreach (var child in _ui.WindowRoot.Children)
        {
            if (child is PaperWindow window)
                Mount(window);
        }
    }

    private void Mount(PaperWindow window)
    {
        var text = Rope.Collapse(window.Input.TextRope);
        if (!text.Contains(Marker))
            return;

        CameraArchivePrintComponent? shot = null;
        string? content = null;
        foreach (var print in _prints)
        {
            if (!text.Contains(print.Content) && !print.Content.Contains(text))
                continue;

            shot = print.Shot;
            content = print.Content;
            break;
        }

        if (shot == null || content == null)
            return;

        var written = FindWritten(window);
        if (written == null)
            return;

        var at = content.IndexOf(Marker, StringComparison.Ordinal);
        if (at < 0)
            return;

        var before = content[..at].TrimEnd();
        var after = content[(at + Marker.Length)..].TrimStart();
        var beforeMessage = new FormattedMessage();
        beforeMessage.AddMarkupPermissive(before);
        written.SetMessage(beforeMessage, UserFormattableTags.BaseAllowedTags, Ink);

        var parent = written.Parent;
        if (parent == null)
            return;

        var image = Find<TextureRect>(parent, "CameraArchiveShot");
        var tail = Find<RichTextLabel>(parent, "CameraArchiveTail");
        if (image == null)
        {
            image = new TextureRect
            {
                Name = "CameraArchiveShot",
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                MinSize = new System.Numerics.Vector2(460, 260),
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
