using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.PdaPainter;

[RegisterComponent, NetworkedComponent]
public sealed partial class PdaPainterComponent : Component
{
    public const string SlotId = "pda_slot";

    /// <summary>
    /// Server-side canvas of drawn pixels. Key is y * CanvasSize + x,
    /// value is the packed ARGB color, or 0 for "no pixel".
    /// </summary>
    [DataField]
    public Dictionary<int, int> Canvas = new();

    /// <summary>
    /// Currently selected template prototype id ("по шаблону" tab), if any.
    /// </summary>
    [DataField]
    public string? SelectedTemplate;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class PdaPaintedComponent : Component
{
    /// <summary>
    /// Pixels painted on the PDA, in the same format as <see cref="PdaPainterComponent.Canvas"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<int, int> Pixels = new();
}
