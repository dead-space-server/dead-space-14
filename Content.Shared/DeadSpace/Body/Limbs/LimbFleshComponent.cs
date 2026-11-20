// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Humanoid.Markings;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Body.Limbs;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class LimbFleshComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Bare;

    [DataField, AutoNetworkedField]
    public Color Skin = Color.White;

    [DataField, AutoNetworkedField]
    public string Species = "";

    [DataField, AutoNetworkedField]
    public float Wound = 1f;

    [DataField, AutoNetworkedField]
    public float Burn = 1f;

    [DataField, AutoNetworkedField]
    public float LiquidSever;

    [DataField, AutoNetworkedField]
    public string DamageSet = "";

    [DataField]
    public string Dna = "";

    [DataField]
    public bool Unstable;

    [DataField]
    public bool Gelatin;

    [ViewVariables]
    public EntityUid? Source;

    [DataField]
    public string Donor = "";

    [DataField, AutoNetworkedField]
    public Color Eyes = Color.White;

    [DataField, AutoNetworkedField]
    public List<Marking> Marks = new();

    [DataField, AutoNetworkedField]
    public string EyeSprite = "";

    [DataField, AutoNetworkedField]
    public string EyeState = "";

    [DataField, AutoNetworkedField]
    public List<string> CarrySprites = new();

    [DataField, AutoNetworkedField]
    public List<string> CarryStates = new();
}
