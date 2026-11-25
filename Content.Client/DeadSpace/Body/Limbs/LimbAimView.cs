// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Input;

namespace Content.Client.DeadSpace.Body.Limbs;

public sealed class LimbAimView : Control
{
    private const string RsiPath = "/Textures/_DeadSpace/Interface/LimbDoll/aimmode.rsi";

    public bool Aimed;

    private RSI? _sheet;

    public event Action<GUIBoundKeyEventArgs>? Pressed;

    public LimbAimView()
    {
        MouseFilter = MouseFilterMode.Stop;
        MinSize = new Vector2(64, 64);
        MaxSize = MinSize;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var sheet = Sheet();
        if (!sheet.TryGetState(Aimed ? "aimed" : "normal", out var state))
            return;

        handle.DrawTextureRect(state.GetFrame(RsiDirection.South, 0), UIBox2.FromDimensions(Vector2.Zero, PixelSize));
    }

    private RSI Sheet()
    {
        return _sheet ??= IoCManager.Resolve<IResourceCache>().GetResource<RSIResource>(RsiPath).RSI;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        Pressed?.Invoke(args);
        args.Handle();
    }
}
