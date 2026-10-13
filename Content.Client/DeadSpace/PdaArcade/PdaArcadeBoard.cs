// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.PdaArcade;
using Content.Shared.Input;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.PdaArcade;

public abstract class PdaArcadeBoard : Control
{
    public event Action<int>? Finished;
    public event Action<int>? Scored;

    public bool IsPaused { get; set; }
    public bool Won { get; private set; }
    public bool IsOver => Over;

    protected int Score;
    protected bool Over;
    private bool _sent;
    private SharedAudioSystem? _audio;

    protected PdaArcadeBoard()
    {
        CanKeyboardFocus = true;
        MouseFilter = MouseFilterMode.Stop;
        RectClipContent = true;
        VerticalExpand = true;
        HorizontalExpand = true;
        MinSize = new System.Numerics.Vector2(180, 180);
        OnKeyBindDown += OnKey;
        OnKeyBindUp += args =>
        {
            if (!TryCommand(args.Function, out var command) || command == "act")
                return;

            args.Handle();
            Command(command, false);
        };
    }

    protected virtual void OnKey(GUIBoundKeyEventArgs args)
    {
        if (!TryCommand(args.Function, out var command))
            return;

        args.Handle();
        Command(command, true);
    }

    protected static bool TryCommand(BoundKeyFunction function, out string command)
    {
        if (function == ContentKeyFunctions.ArcadeLeft || function == EngineKeyFunctions.MoveLeft || function == EngineKeyFunctions.TextCursorLeft)
            command = "left";
        else if (function == ContentKeyFunctions.ArcadeRight || function == EngineKeyFunctions.MoveRight || function == EngineKeyFunctions.TextCursorRight)
            command = "right";
        else if (function == ContentKeyFunctions.ArcadeUp || function == EngineKeyFunctions.MoveUp || function == EngineKeyFunctions.TextCursorUp)
            command = "up";
        else if (function == ContentKeyFunctions.ArcadeDown || function == EngineKeyFunctions.MoveDown || function == EngineKeyFunctions.TextCursorDown)
            command = "down";
        else if (function == ContentKeyFunctions.Arcade1 || function == EngineKeyFunctions.Use)
            command = "act";
        else
        {
            command = "";
            return false;
        }

        return true;
    }

    public virtual void Command(string command, bool down)
    {
    }

    public void Restart()
    {
        Score = 0;
        Over = false;
        Won = false;
        IsPaused = false;
        _sent = false;
        OnRestart();
        Sound(PdaArcadeArt.Profile.Start);
    }

    protected abstract void OnRestart();

    public void FinishNow()
    {
        IsPaused = false;
        End(false);
    }

    public int Bank()
    {
        Over = true;
        _sent = true;
        return Score;
    }

    protected void End(bool won = false)
    {
        if (Over)
            return;

        Over = true;
        Won = won;
        Sound(won ? PdaArcadeArt.Profile.Win : PdaArcadeArt.Profile.Lose);
        if (_sent)
            return;

        _sent = true;
        Finished?.Invoke(Score);
    }

    protected void Gain(int amount)
    {
        Score += amount;
        Scored?.Invoke(Score);
        Sound(PdaArcadeArt.Profile.Action);
    }

    protected void Sound(SoundSpecifier sound)
    {
        _audio ??= IoCManager.Resolve<IEntityManager>().System<SharedAudioSystem>();
        _audio.PlayGlobal(sound, Filter.Local(), false, AudioParams.Default.WithVolume(-6f));
    }

    private string _gameId = "";

    public void Bind(string gameId)
    {
        _gameId = gameId;
    }

    protected PdaArcadeGamePrototype Visual => PdaArcadeArt.Game(_gameId);

    protected Texture Sprite(string key)
    {
        return PdaArcadeArt.Texture(Visual.Sprites[key]);
    }

    protected void DrawFloor(DrawingHandleScreen handle, int index = 0)
    {
        SpriteSpecifier? floor = null;
        if (Visual.Floors.Count > 0)
            floor = Visual.Floors[index % Visual.Floors.Count];
        else
            floor = Visual.Floor;

        if (floor == null)
            return;

        TileFloor(handle, floor);
    }

    protected static bool Key(GUIBoundKeyEventArgs args, BoundKeyFunction function)
    {
        return args.Function == function;
    }

    protected void TileFloor(DrawingHandleScreen handle, SpriteSpecifier floor)
    {
        var tex = PdaArcadeArt.Texture(floor);
        const float tile = 32f;
        for (var y = 0f; y < PixelSize.Y; y += tile)
        for (var x = 0f; x < PixelSize.X; x += tile)
        {
            handle.DrawTextureRect(tex, new UIBox2(x, y, MathF.Min(x + tile, PixelSize.X), MathF.Min(y + tile, PixelSize.Y)));
        }
    }

    protected void FillBackground(DrawingHandleScreen handle, Color color)
    {
        handle.DrawRect(new UIBox2(0, 0, PixelSize.X, PixelSize.Y), color);
    }

    protected UIBox2 Inner(float margin)
    {
        return new UIBox2(margin, margin, Math.Max(margin + 1, PixelSize.X - margin), Math.Max(margin + 1, PixelSize.Y - margin));
    }
}

