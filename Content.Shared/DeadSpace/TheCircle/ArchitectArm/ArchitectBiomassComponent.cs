// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.TheCircle.ArchitectArm;

/// <summary>
/// The Architect drains a corpse's biomass (alt-click on the body): the arm gets matter back and the Architect's own
/// wounds close a little.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ArchitectBiomassComponent : Component
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Matter returned to the arm.
    /// </summary>
    [DataField]
    public int Charges = 5;

    /// <summary>
    /// Bone damage mended on each limb (a full break is 40).
    /// </summary>
    [DataField]
    public float BoneMend = 8f;

    /// <summary>
    /// Bleeding stopped on each limb.
    /// </summary>
    [DataField]
    public float BleedMend = 0.3f;

    /// <summary>
    /// Damage taken off each organ that is not torn apart.
    /// </summary>
    [DataField]
    public float OrganMend = 10f;
}

/// <summary>
/// A corpse whose biomass is already gone.
/// </summary>
[RegisterComponent]
public sealed partial class ArchitectBiomassDrainedComponent : Component;

[Serializable, NetSerializable]
public sealed partial class ArchitectAbsorbBiomassDoAfterEvent : SimpleDoAfterEvent;
