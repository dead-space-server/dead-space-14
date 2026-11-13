
namespace Content.Server.NPC.HTN;

/// <summary>
/// Represents a network of multiple tasks. This gets expanded out to its relevant nodes.
/// </summary>
/// <remarks>
/// This just points to a specific htnCompound prototype
/// </remarks>
public sealed partial class HTNCompoundTask : HTNTask, IHTNCompound
{
    [DataField("task", required: true)]
    public string Task = string.Empty;
}
