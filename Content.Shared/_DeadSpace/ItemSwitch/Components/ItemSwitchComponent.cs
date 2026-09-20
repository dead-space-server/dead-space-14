using Content.Shared.DeadSpace.ItemSwitch;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.ItemSwitch.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
[Access(typeof(SharedItemSwitchSystem))]
public sealed partial class ItemSwitchComponent : Component
{
    /// <summary>
    ///     The item's toggle state.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string State;

    [DataField(readOnly: true)]
    public Dictionary<string, ItemSwitchState> States = new();

    /// <summary>
    ///     Can the entity be activated in the world.
    /// </summary>
    [DataField]
    public bool OnActivate = true;

    /// <summary>
    ///     If this is set to false then the item can't be toggled by pressing Z.
    ///     Use another system to do it then.
    /// </summary>
    [DataField]
    public bool OnUse = true;

    /// <summary>
    ///     Whether the item's toggle can be predicted by the client.
    /// </summary>
    [DataField]
    public bool Predictable = true;

    /// <summary>
    ///     Whether the item's currently toggled state should be shown in the UI.
    /// </summary>
    [DataField]
    public bool ShowLabel;
}

[DataDefinition]
public sealed partial class ItemSwitchState
{
    [DataField]
    public string? Verb;

    [DataField]
    public SoundSpecifier? SoundStateActivate;

    [DataField]
    public SoundSpecifier? SoundFailToActivate;

    [DataField]
    public ComponentRegistry? Components;

    [DataField]
    public bool RemoveComponents = true;

    [DataField]
    public SpriteSpecifier? Sprite;

    [DataField]
    public bool Hidden;
}

/// <summary>
/// Raised directed on an entity when its item is attempted to be toggled.
/// </summary>
[ByRefEvent]
public record struct ItemSwitchAttemptEvent()
{
    public bool Cancelled = false;
    public required EntityUid? User { get; init; }
    public required string State { get; init; }

    /// <summary>
    /// Pop-up that gets shown to users explaining why the attempt was cancelled.
    /// </summary>
    public string? Popup { get; set; }
}

/// <summary>
/// Raised directed on an entity any sort of toggle is complete.
/// </summary>
[ByRefEvent]
public readonly record struct ItemSwitchedEvent
{
    public required bool Predicted { get; init; }
    public required string State { get; init; }
    public required EntityUid? User { get; init; }
}

/// <summary>
///     Visuals raised when an item switch state changes.
/// </summary>
public enum ItemSwitchVisuals : byte
{
    Switched
}

/// <summary>
///     Verb category used for item switch state selection.
/// </summary>
public static class ItemSwitchVerbCategory
{
    public static readonly VerbCategory Switch = new("verb-categories-switch", null);
}