using Content.Shared.NameModifier.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Shared.NameIdentifier;

/// <summary>
///     Handles unique name identifiers for entities e.g. `monkey (MK-912)`
/// </summary>
public abstract class SharedNameIdentifierSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    // DS14-start: retain explicit subscriptions used by the current shared system layout.
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NameIdentifierComponent, RefreshNameModifiersEvent>(OnRefreshNameModifiers);
    }
    // DS14-end

    private void OnRefreshNameModifiers(Entity<NameIdentifierComponent> ent, ref RefreshNameModifiersEvent args)
    {
        if (ent.Comp.Group is null)
            return;

        // Don't add whitespace
        if (string.IsNullOrWhiteSpace(ent.Comp.FullIdentifier))
            return;

        // Don't apply the modifier if the component is being removed
        if (ent.Comp.LifeStage > ComponentLifeStage.Running)
            return;

        if (!_prototypeManager.Resolve(ent.Comp.Group, out var group))
            return;

        var format = "name-identifier-format-full";

        if (!group.FullName)
            format = group.Prefix ? "name-identifier-format-prepend" : "name-identifier-format-append";

        // We apply the modifier with a low priority to keep it near the base name
        // "Beep (Si-4562) the zombie" instead of "Beep the zombie (Si-4562)"
        args.AddModifier(format, -10, ("identifier", ent.Comp.FullIdentifier));
    }
}
