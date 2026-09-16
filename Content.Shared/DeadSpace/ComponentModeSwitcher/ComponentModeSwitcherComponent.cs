// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.ComponentModeSwitcher;

/// <summary>
/// Cycles an entity through an arbitrary number of prototype-defined component sets on Alt+RMB.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ComponentModeSwitcherComponent : Component
{
    /// <summary>
    /// Ordered modes. Alt+RMB advances to the next entry and wraps back to zero.
    /// </summary>
    [DataField(required: true)]
    public List<ComponentModeSwitcherMode> Modes = new();

    /// <summary>
    /// Index of the currently applied mode.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int CurrentMode;

    /// <summary>
    /// Whether runtime component values are restored when returning to a mode.
    /// This preserves values such as ammunition, charges, and the selected setting.
    /// </summary>
    [DataField]
    public bool PreserveState = true;

    /// <summary>
    /// Component snapshots for modes that have already been left.
    /// These are server-side runtime data; active components are replicated normally.
    /// </summary>
    public readonly Dictionary<int, ComponentRegistry> SavedStates = new();
}

[DataDefinition]
public sealed partial class ComponentModeSwitcherMode
{
    /// <summary>
    /// Localized mode name. It is also used as the default switch popup.
    /// </summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// Components that exist while this mode is active.
    /// </summary>
    [DataField(required: true)]
    public ComponentRegistry Components = new();

    /// <summary>
    /// Optional popup shown to the user after switching to this mode.
    /// </summary>
    [DataField]
    public LocId? Popup;
}

[ByRefEvent]
public readonly record struct ComponentModeChangedEvent(int PreviousMode, int CurrentMode, LocId Name, EntityUid? User);