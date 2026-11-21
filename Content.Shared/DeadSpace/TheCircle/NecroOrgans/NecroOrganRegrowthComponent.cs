// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Body.Limbs;

namespace Content.Shared.DeadSpace.TheCircle.NecroOrgans;

/// <summary>
/// A burst organ is thrown out and a necromorph organ grows in its place. Each organ can be replaced once per
/// <see cref="Cooldown"/>; necromorph organs burst <see cref="NecroRuptureMultiplier"/> times as easily.
/// </summary>
[RegisterComponent]
public sealed partial class NecroOrganRegrowthComponent : Component
{
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(60);

    [DataField]
    public float NecroRuptureMultiplier = 2f;

    /// <summary>
    /// Organs that already are necromorph ones.
    /// </summary>
    [ViewVariables]
    public LimbOrganMask Necro;

    /// <summary>
    /// When each organ may be replaced again.
    /// </summary>
    [ViewVariables]
    public Dictionary<LimbOrgan, TimeSpan> NextRegrow = new();

    [ViewVariables]
    public TimeSpan NextCheck;
}
