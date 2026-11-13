using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Damage
{
    /// <summary>
    ///     A set of coefficients or flat modifiers to damage types. Can be applied to <see cref="DamageSpecifier"/> using <see
    ///     cref="DamageSpecifier.ApplyModifierSet(DamageSpecifier, DamageModifierSet)"/>. This can be done several times as the
    ///     <see cref="DamageSpecifier"/> is passed to it's final target. By default the receiving <see cref="DamageableComponent"/>, will
    ///     also apply it's own <see cref="DamageModifierSet"/>.
    /// </summary>
    /// <remarks>
    /// The modifier will only ever be applied to damage that is being dealt. Healing is unmodified.
    /// </remarks>
    [DataDefinition]
    [Serializable, NetSerializable]
    [Virtual]
    public partial class DamageModifierSet
    {
        [DataField("coefficients")]
        public Dictionary<string, float> Coefficients = new();

        [DataField("flatReductions")]
        public Dictionary<string, float> FlatReduction = new();

        // DS14-Start
        [DataField]
        public Dictionary<string, int> ArmorLvls = new();
        // DS14-End
    }
}
