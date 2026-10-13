namespace Content.Server.DeadSpace.Weapons.Melee;

[RegisterComponent]
public sealed partial class AnimationOnMeleeComponent : Component
{
    [DataField]
    public string Emote = "EmoteFlip";
}
