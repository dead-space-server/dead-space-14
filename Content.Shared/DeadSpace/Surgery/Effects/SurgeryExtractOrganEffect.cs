namespace Content.Shared.DeadSpace.Surgery.Effects;

public sealed partial class SurgeryExtractOrganEffect : SurgeryEffectBase<SurgeryExtractOrganEffect>
{
    [DataField]
    public int Amount = 1;

    [DataField]
    public float DamageTransfer = 0.25f;

    [DataField]
    public bool DropAtPatient = true;
}
