// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Item;
using Content.Shared.Weapons.Melee;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.Lavaland;

/// <summary>Which of the pair is shown: Cain (red, the dash to the dagger) or Abel (blue, the ring of ice).</summary>
[Serializable, NetSerializable]
public enum LavalandCainAndAbelVisuals : byte
{
    Cain,
}

[Serializable, NetSerializable]
public enum LavalandCainAndAbelLayers : byte
{
    Base,
}

/// <summary>
/// Which blade of Cain and Abel is in front, known on both sides: wielding them shows the right pair at once (the
/// client predicts the wield), and Cain held in both hands flings its blade out on a chain.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class LavalandCainAndAbelLookComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Cain;

    /// <summary>How far out the swing is drawn with Cain in both hands (the blade on its chain).</summary>
    [DataField]
    public float CainWieldedAnimationOffset = 2.4f;

    /// <summary>The swing's reach otherwise, as the prototype gave it.</summary>
    [DataField]
    public float AnimationOffset = 1f;
}

/// <summary>A swing drawn with a chain from the wielder to the blade (Cain flung out in both hands).</summary>
[RegisterComponent]
public sealed partial class LavalandChainArcComponent : Component
{
    [DataField(required: true)]
    public SpriteSpecifier Sprite = default!;

    [DataField]
    public Color Color = Color.White;
}

public sealed class LavalandCainAndAbelLookSystem : EntitySystem
{
    [Dependency] private readonly SharedItemSystem _item = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandCainAndAbelLookComponent, ItemWieldedEvent>(OnWielded, after: [typeof(SharedWieldableSystem)]);
        SubscribeLocalEvent<LavalandCainAndAbelLookComponent, ItemUnwieldedEvent>(OnUnwielded, after: [typeof(SharedWieldableSystem)]);
        SubscribeLocalEvent<LavalandCainAndAbelLookComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    // Wielding sets its own prefix: the right one for the blade in front goes over it.
    private void OnWielded(Entity<LavalandCainAndAbelLookComponent> ent, ref ItemWieldedEvent args)
    {
        UpdateLook(ent, true);
    }

    private void OnUnwielded(Entity<LavalandCainAndAbelLookComponent> ent, ref ItemUnwieldedEvent args)
    {
        UpdateLook(ent, false);
    }

    private void OnHandleState(Entity<LavalandCainAndAbelLookComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateLook(ent);
    }

    public void SetCain(Entity<LavalandCainAndAbelLookComponent> ent, bool cain)
    {
        ent.Comp.Cain = cain;
        Dirty(ent);
        UpdateLook(ent);
    }

    public void UpdateLook(Entity<LavalandCainAndAbelLookComponent> ent, bool? wielded = null)
    {
        wielded ??= TryComp<WieldableComponent>(ent, out var wieldable) && wieldable.Wielded;
        var cain = ent.Comp.Cain;
        _item.SetHeldPrefix(ent, (cain, wielded.Value) switch
        {
            (true, true) => "cain-wielded",
            (true, false) => "cain",
            (false, true) => "wielded",
            _ => null,
        });

        if (TryComp<MeleeWeaponComponent>(ent, out var melee))
            melee.AnimationOffset = cain && wielded.Value ? ent.Comp.CainWieldedAnimationOffset : ent.Comp.AnimationOffset;
    }
}
