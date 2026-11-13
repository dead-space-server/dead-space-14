// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using System.Numerics;
using Content.Client.Items.Systems;
using Content.Client.TextScreen;
using Content.Shared.DeadSpace.SignBoard;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.TextScreen;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.GameObjects;

namespace Content.Client.DeadSpace.SignBoard;

public sealed partial class SignBoardVisualsSystem : EntitySystem
{
    [Dependency] private ItemSystem _itemSystem = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private Angle _lastEyeAngle;

    public override void Initialize()
    {
        SubscribeLocalEvent<SignBoardComponent, GetInhandVisualsEvent>(OnGetInhandVisuals);
        SubscribeLocalEvent<SignBoardComponent, AppearanceChangeEvent>(OnAppearanceChanged);
        _transform.OnGlobalMoveEvent += OnGlobalMove;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _transform.OnGlobalMoveEvent -= OnGlobalMove;
    }

    private void OnGlobalMove(ref MoveEvent args)
    {
        var rotDiff = (args.NewRotation - args.OldRotation).Reduced();
        if (Math.Abs(rotDiff.Theta) < 0.001)
            return;

        if (!TryComp<HandsComponent>(args.Sender, out var hands))
            return;

        foreach (var held in _hands.EnumerateHeld((args.Sender, hands)))
        {
            if (HasComp<SignBoardComponent>(held))
            {
                _itemSystem.VisualsChanged(held);
                return;
            }
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        var player = _playerManager.LocalEntity;
        if (player == null)
            return;

        var eyeAngle = _eye.CurrentEye.Rotation;
        if (Math.Abs(eyeAngle.Theta - _lastEyeAngle.Theta) < 0.001)
            return;

        _lastEyeAngle = eyeAngle;

        var query = EntityQueryEnumerator<SignBoardComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            _itemSystem.VisualsChanged(uid);
        }
    }

    private void OnAppearanceChanged(EntityUid uid, SignBoardComponent component, ref AppearanceChangeEvent args)
    {
        _itemSystem.VisualsChanged(uid);
    }

    private void OnGetInhandVisuals(EntityUid uid, SignBoardComponent sign, GetInhandVisualsEvent args)
    {
        if (!TryComp<TextScreenVisualsComponent>(uid, out var screen))
            return;

        var text = sign.Text;

        if (string.IsNullOrEmpty(text))
            return;

        var rotation = Transform(args.User).WorldRotation + _eye.CurrentEye.Rotation;
        if (rotation.GetCardinalDir() != Direction.South)
            return;

        var pixelSize = TextScreenVisualsComponent.PixelSize;
        var charWidth = 4;
        var rows = screen.Rows;
        var rowLength = screen.RowLength;
        var rowOffset = screen.RowOffset;

        var rowCount = Math.Min(rows, (text.Length - 1) / rowLength + 1);
        for (var rowIdx = 0; rowIdx < rowCount; rowIdx++)
        {
            var start = rowIdx * rowLength;
            var len = Math.Min(text.Length - start, rowLength);
            var row = text.Substring(start, len).Trim();
            if (string.IsNullOrEmpty(row))
                continue;

            for (var chr = 0; chr < row.Length; chr++)
            {
                var state = TextScreenSystem.GetStateFromChar(row[chr]);
                if (state == null)
                    continue;

                var charLayer = new PrototypeLayerData
                {
                    RsiPath = "Effects/text.rsi",
                    State = state,
                    Color = screen.Color,
                    Offset = Vector2.Multiply(
                        new Vector2((chr - row.Length / 2f + 0.5f) * charWidth, -rowIdx * rowOffset),
                        pixelSize) + screen.TextOffset
                };

                var key = $"signboard-text-{args.Location.ToString().ToLowerInvariant()}-{rowIdx}-{chr}";
                args.Layers.Add((key, charLayer));
            }
        }
    }
}
