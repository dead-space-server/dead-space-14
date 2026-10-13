// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.PdaArcade;

public sealed class PdaFlapBoard : PdaArcadeBoard
{
    private readonly List<float> _pipes = new();
    private readonly List<float> _gaps = new();
    private float _bird = 80f;
    private float _vy;
    private readonly Random _random = new();

    private float _time;

    public override void Command(string command, bool down)
    {
        if (down)
            Flap();
    }

    public void Flap()
    {
        if (Over)
            return;

        _vy = -150f;
        Sound(PdaArcadeArt.Profile.Action);
    }

    protected override void OnRestart()
    {
        _pipes.Clear();
        _gaps.Clear();
        _bird = 80f;
        _vy = 0f;
        _time = 0f;
        for (var i = 0; i < 3; i++)
            AddPipe(160f + i * 90f);
    }

    protected override void OnKey(GUIBoundKeyEventArgs args)
    {
        base.OnKey(args);
        if (Key(args, EngineKeyFunctions.TextCursorUp) || Key(args, EngineKeyFunctions.MoveUp) || Key(args, EngineKeyFunctions.Use) || Key(args, EngineKeyFunctions.Use))
            Flap();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (Over || IsPaused)
            return;

        var dt = args.DeltaSeconds;
        _time += dt;
        var pace = MathF.Min(2.4f, 1f + _time * 0.018f);
        _vy += 280f * dt;
        _bird += _vy * dt;
        if (_bird < 6f || _bird > 154f)
        {
            End();
            return;
        }

        for (var i = 0; i < _pipes.Count; i++)
        {
            var before = _pipes[i];
            _pipes[i] -= 55f * pace * dt;
            if (before > BirdX && _pipes[i] <= BirdX)
                Gain(10);

            if (HitsPipe(_pipes[i], _gaps[i]))
            {
                End();
                return;
            }
        }

        if (_pipes.Count > 0 && _pipes[0] < -20f)
        {
            _pipes.RemoveAt(0);
            _gaps.RemoveAt(0);
            AddPipe(_pipes.Count == 0 ? 160f : _pipes[^1] + 90f);
        }
    }

    private void AddPipe(float x)
    {
        _pipes.Add(x);
        _gaps.Add(40f + _random.Next(80));
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        DrawFloor(handle, (int) (_time / 12f));
        var inner = Inner(6);
        var scale = Math.Min(inner.Width, inner.Height) / 160f;
        if (scale <= 0f)
            return;

        var originX = inner.Left + (inner.Width - 160 * scale) / 2f;
        var originY = inner.Top + (inner.Height - 160 * scale) / 2f;
        var pipe = Sprite("pipe");
        var hamlet = Sprite("creature");
        for (var i = 0; i < _pipes.Count; i++)
        {
            var x = originX + _pipes[i] * scale;
            var gap = originY + _gaps[i] * scale;
            DrawPipe(handle, pipe, new UIBox2(x, originY, x + 16 * scale, gap - 26 * scale));
            DrawPipe(handle, pipe, new UIBox2(x, gap + 26 * scale, x + 16 * scale, originY + 160 * scale));
        }

        handle.DrawTextureRect(hamlet, new UIBox2(originX + (BirdX - 12f) * scale, originY + (_bird - 11f) * scale, originX + (BirdX + 12f) * scale, originY + (_bird + 11f) * scale));
    }

    private const float BirdX = 42f;

    private bool HitsPipe(float pipeX, float gap)
    {
        var birdLeft = BirdX - 12f + 24f * (10f / 32f) + 0.6f;
        var birdRight = BirdX - 12f + 24f * (21f / 32f) - 0.6f;
        var birdTop = _bird - 11f + 22f * (9f / 32f) + 0.4f;
        var birdBottom = _bird - 11f + 22f * (19f / 32f) - 0.4f;
        var pipeLeft = pipeX + 16f * (13f / 32f);
        var pipeRight = pipeX + 16f * (18f / 32f);
        if (birdRight < pipeLeft || birdLeft > pipeRight)
            return false;

        const float mouth = 26f;
        return birdTop < gap - mouth || birdBottom > gap + mouth;
    }

    private static void DrawPipe(DrawingHandleScreen handle, Texture pipe, UIBox2 column)
    {
        if (column.Height < 2f || column.Width < 2f)
            return;

        for (var y = column.Top; y < column.Bottom; y += 16f)
        {
            var bottom = MathF.Min(column.Bottom, y + 16f);
            handle.DrawTextureRect(pipe, new UIBox2(column.Left, y, column.Right, bottom));
        }
    }
}
