// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.Psychiatry;

[RegisterComponent]
public sealed partial class PsychiatryRemapComponent : Component
{
    [ViewVariables]
    public string FakeName = string.Empty;

    [ViewVariables]
    public ResPath? OriginalRsi;

    [ViewVariables]
    public string? OriginalState;

    [ViewVariables]
    public List<bool>? LayerVisibility;

    [ViewVariables]
    public bool IsWall;

    [ViewVariables]
    public bool IsFloor;

    [ViewVariables]
    public Vector2i? TileIndex;

    [ViewVariables]
    public string? RemapId;

    [ViewVariables]
    public ResPath DrawRsi;

    [ViewVariables]
    public string DrawState = string.Empty;

    [ViewVariables]
    public Color DrawColor = Color.White;

    [ViewVariables]
    public Dictionary<string, bool> OriginalLayerVisible = new();
}
