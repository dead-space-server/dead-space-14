// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Lavaland;

/// <summary>
/// Fauna raids on the salvage outpost. Every <see cref="CheckInterval"/> a raid is rolled with the current chance;
/// a failed roll raises the chance for the next one, a raid resets it to <see cref="BaseChance"/>.
/// </summary>
[DataDefinition]
public sealed partial class LavalandFaunaRaidConfig
{
    [DataField]
    public bool Enabled = true;

    [DataField]
    public TimeSpan CheckInterval = TimeSpan.FromMinutes(40);

    [DataField]
    public float BaseChance = 0.25f;

    /// <summary>Chance added after a failed roll, picked between min and max.</summary>
    [DataField]
    public float ChanceIncreaseMin = 0.15f;

    [DataField]
    public float ChanceIncreaseMax = 0.35f;

    [DataField]
    public TimeSpan WarningDuration = TimeSpan.FromMinutes(1);

    [DataField]
    public TimeSpan Duration = TimeSpan.FromMinutes(3);

    /// <summary>Tendrils spawn this far from the outpost.</summary>
    [DataField]
    public float MinDistance = 12f;

    [DataField]
    public float MaxDistance = 20f;

    [DataField]
    public List<EntProtoId> Tendrils = new();

    [DataField]
    public SoundSpecifier? WarningSound;

    [DataField]
    public List<SoundSpecifier> Music = new();
}
