using System.Diagnostics.CodeAnalysis;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared.Cloning;

public abstract partial class SharedCloningSystem : EntitySystem
{
    /// <summary>
    /// Spawns a clone of the given humanoid at the specified coordinates or in nullspace.
    /// </summary>
    public virtual bool TryCloning(
        EntityUid original,
        MapCoordinates? coords,
        ProtoId<CloningSettingsPrototype> settingsId,
        [NotNullWhen(true)] out EntityUid? clone)
    {
        clone = null;
        return false;
    }

    /// <summary>
    /// Copy components from one entity to another based on a CloningSettingsPrototype.
    /// </summary>
    /// <param name="original">The orignal Entity to clone components from.</param>
    /// <param name="clone">The target Entity to clone components to.</param>
    /// <param name="settings">The clone settings prototype containing the list of components to clone.</param>
    public virtual void CloneComponents(EntityUid original, EntityUid clone, CloningSettingsPrototype settings)
    {
    }

    /// <summary>
    /// Copy components from one entity to another based on a CloningSettingsPrototype.
    /// </summary>
    /// <param name="original">The orignal Entity to clone components from.</param>
    /// <param name="clone">The target Entity to clone components to.</param>
    /// <param name="settings">The clone settings prototype id containing the list of components to clone.</param>
    public virtual void CloneComponents(EntityUid original, EntityUid clone, ProtoId<CloningSettingsPrototype> settings)
    {
    }

    /// <summary>
    /// Copies permanent status effects from one entity to another.
    /// </summary>
    public virtual void CopyStatusEffects(
        EntityUid original,
        EntityUid target,
        EntityWhitelist? whitelist = null,
        EntityWhitelist? blacklist = null)
    {
    }
}
