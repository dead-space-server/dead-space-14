using System.Numerics;
using Content.Shared.Storage;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.DeadSpace.UserInterface.Storage;

/// <summary>
/// Рисует обводку вокруг каждой группы сетки хранилища.
/// </summary>
public sealed class StorageGridBorderOverlay : Control
{
    /// <summary>
    /// Список групп. Каждая группа — набор боксов, считающихся одним целым.
    /// </summary>
    public List<List<Box2i>> Groups = new();

    /// <summary>
    /// Размер одной клетки в пикселях (без UIScale).
    /// </summary>
    public Vector2 CellSize = Vector2.One;

    /// <summary>
    /// Общая нижне-левая точка bounding box всей сетки — чтобы вычислять смещение.
    /// </summary>
    public Vector2i Origin = Vector2i.Zero;

    public Color BorderColor = Color.FromHex("#77777733");
    public float Thickness = 1f;

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (Groups.Count == 0 || CellSize == Vector2.Zero)
            return;

        var t = Thickness * UIScale;
        var size = CellSize * UIScale;

        foreach (var group in Groups)
        {
            if (group.Count == 0)
                continue;

            var cells = new HashSet<Vector2i>();
            foreach (var box in group)
            {
                for (var x = box.Left; x <= box.Right; x++)
                    for (var y = box.Bottom; y <= box.Top; y++)
                        cells.Add(new Vector2i(x, y));
            }

            foreach (var cell in cells)
            {
                var topOpen    = !cells.Contains(cell - Vector2i.Up);
                var bottomOpen = !cells.Contains(cell + Vector2i.Up);
                var leftOpen   = !cells.Contains(cell + Vector2i.Left);
                var rightOpen  = !cells.Contains(cell + Vector2i.Right);

                if (!topOpen && !bottomOpen && !leftOpen && !rightOpen)
                    continue;

                var local = new Vector2(cell.X - Origin.X, cell.Y - Origin.Y) * size;
                var tl = PixelPosition + local;
                var br = tl + size;

                if (topOpen)
                    handle.DrawRect(new UIBox2(tl.X, tl.Y, br.X, tl.Y + t), BorderColor);
                if (bottomOpen)
                    handle.DrawRect(new UIBox2(tl.X, br.Y - t, br.X, br.Y), BorderColor);
                if (leftOpen)
                    handle.DrawRect(new UIBox2(tl.X, tl.Y, tl.X + t, br.Y), BorderColor);
                if (rightOpen)
                    handle.DrawRect(new UIBox2(br.X - t, tl.Y, br.X, br.Y), BorderColor);
            }
        }
    }
}
