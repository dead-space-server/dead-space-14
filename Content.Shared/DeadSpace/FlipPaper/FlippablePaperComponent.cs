using Content.Shared.Paper;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.FlipPaper;

/// <summary>
/// Двусторонний листок бумаги. При переворачивании (вердом, как игральная карта)
/// заменяется на прототип <see cref="FlipTarget"/>, а текст со штампами
/// переносится на новую сторону листа.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FlippablePaperComponent : Component
{
    /// <summary>
    /// Прототип сущности, которая заменит эту при перевороте.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public EntProtoId FlipTarget = default!;

    /// <summary>
    /// "Чемоданчик" для текста и штампов: если текущая сторона не имеет
    /// компонента Paper (чистая сторона), текст хранится здесь и
    /// восстанавливается при следующем перевороте.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string StashedContent = "";

    [DataField, AutoNetworkedField]
    public List<string> StashedSignatures = new();

    [DataField, AutoNetworkedField]
    public List<StampDisplayInfo> StashedStampedBy = new();

    [DataField, AutoNetworkedField]
    public string? StashedStampState;
}
