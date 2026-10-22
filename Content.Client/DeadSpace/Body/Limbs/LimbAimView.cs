// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Body.Limbs;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client.DeadSpace.Body.Limbs;

public sealed class LimbAimView : Control
{
    public bool Aimed;

    public LimbAimView()
    {
        MouseFilter = MouseFilterMode.Stop;
        MinSize = new Vector2(32, 32);
        MaxSize = MinSize;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var color = Aimed ? LimbDollPalette.Picked : Color.White;
        var dots = Aimed ? 2 : 6;
        var span = Aimed ? 6f : 12f;
        var center = PixelSize / 2f;
        for (var i = 0; i < dots; i++)
        {
            var angle = i / (float) dots * MathF.Tau;
            var at = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * span;
            handle.DrawRect(UIBox2.FromDimensions(at - new Vector2(2, 2), new Vector2(4, 4)), color);
        }
    }
}
