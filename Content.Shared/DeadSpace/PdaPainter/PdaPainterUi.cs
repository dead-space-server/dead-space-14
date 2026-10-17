using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.PdaPainter;

[Serializable, NetSerializable]
public enum PdaPainterUiKey : byte
{
    Key,
}

/// <summary>
/// A template entry sent to the client for the "по шаблону" tab.
/// Represents any entity prototype with a PdaComponent.
/// </summary>
[Serializable, NetSerializable]
public sealed class PdaPainterTemplateInfo
{
    public PdaPainterTemplateInfo(string id, string name)
    {
        Id = id;
        Name = name;
    }

    /// <summary>Entity prototype id of the PDA.</summary>
    public string Id;
    /// <summary>Localized display name.</summary>
    public string Name;
}

[Serializable, NetSerializable]
public sealed class PdaPainterBoundUserInterfaceState : BoundUserInterfaceState
{
    /// <summary>
    /// Server canvas, same format as <see cref="PdaPainterComponent.Canvas"/>.
    /// </summary>
    public Dictionary<int, int> Canvas;

    public List<PdaPainterTemplateInfo> Templates;

    /// <summary>Id of the currently selected template, if any.</summary>
    public string? SelectedTemplate;

    /// <summary>Entity prototype id of the inserted PDA, used as the free-draw preview backdrop.</summary>
    public string? PreviewProtoId;

    /// <summary>Localized name of the inserted PDA.</summary>
    public string? PreviewName;

    public PdaPainterBoundUserInterfaceState(
        Dictionary<int, int> canvas,
        List<PdaPainterTemplateInfo> templates,
        string? selectedTemplate,
        string? previewProtoId,
        string? previewName)
    {
        Canvas = canvas;
        Templates = templates;
        SelectedTemplate = selectedTemplate;
        PreviewProtoId = previewProtoId;
        PreviewName = previewName;
    }
}

/// <summary>
/// A batch of painted pixels from the client canvas.
/// Color value 0 means "erase" (no pixel).
/// </summary>
[Serializable, NetSerializable]
public sealed class PdaPainterDrawMessage(List<int> indices, List<int> colors) : BoundUserInterfaceMessage
{
    public List<int> Indices { get; } = indices;
    public List<int> Colors { get; } = colors;
}

[Serializable, NetSerializable]
public sealed class PdaPainterSaveMessage(string? templateId) : BoundUserInterfaceMessage
{
    public string? TemplateId { get; } = templateId;
}

[Serializable, NetSerializable]
public sealed class PdaPainterResetMessage : BoundUserInterfaceMessage;
