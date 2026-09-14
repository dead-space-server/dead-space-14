using System.Numerics;
using Robust.Shared.Utility;
namespace Content.Client._CM14.Attachable.Components;
[RegisterComponent, AutoGenerateComponentState]
[Access(typeof(AttachableHolderVisualsSystem))]
public sealed partial class AttachableVisualsComponent : Component
{
    [DataField, AutoNetworkedField]
    public ResPath? Rsi;
    [DataField, AutoNetworkedField]
    public string? Prefix;
    [DataField, AutoNetworkedField]
    public string? Suffix = "_a";
    [DataField, AutoNetworkedField]
    public bool IncludeSlotName;
    [DataField, AutoNetworkedField]
    public bool ShowActive;
    [DataField, AutoNetworkedField]
    public int Layer;
    [DataField, AutoNetworkedField]
    public Vector2 Offset;
}
