// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.DeadSpace.DebugAnalyzer;

/// <summary>
/// A hand-held debug scanner. Shows everything the game knows about a living being: health, hunger, thirst and the
/// needs, growth and sex of animals. Handled by DebugAnalyzerSystem.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class DebugAnalyzerComponent : Component
{
    /// <summary>
    /// The being that is being tracked, the report is refreshed until it goes out of range or the window is closed.
    /// </summary>
    [ViewVariables]
    public EntityUid? ScannedEntity;

    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextUpdate;

    /// <summary>
    /// How far from the scanner the being can be while it is tracked. Null means infinite.
    /// </summary>
    [DataField]
    public float? MaxScanRange = 5f;
}

[Serializable, NetSerializable]
public enum DebugAnalyzerUiKey : byte
{
    Key,
}
