// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage;
using Robust.Shared.Audio;

namespace Content.Server.DeadSpace.Implants.Revive.Components;

[RegisterComponent]
public sealed partial class ReviveImplantComponent : Component
{
    [DataField]
    public TimeSpan InjectTime = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan WritheDuration = TimeSpan.FromSeconds(4);

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField, AutoNetworkedField]
    public SoundSpecifier ImplantedSound = new SoundPathSpecifier("/Audio/_DeadSpace/Autosurgeon/sound_weapons_circsawhit.ogg");

    [DataField]
    public int ReviveDamage = 3;

    [DataField]
    public float ThresholdRevive = 175f;

    [DataField]
    public DamageSpecifier HealCount = new()
    {
        DamageDict =
        {
            ["Blunt"] = -2.5f,
            ["Slash"] = -2.5f,
            ["Piercing"] = -2.5f,
            ["Asphyxiation"] = -2.5f,
            ["Bloodloss"] = -8.0f,
            ["Caustic"] = -2.0f,
            ["Cold"] = -1.5f,
            ["Heat"] = -1.5f,
            ["Shock"] = -1.5f,
            ["Poison"] = -2.0f

        }
    };
}
