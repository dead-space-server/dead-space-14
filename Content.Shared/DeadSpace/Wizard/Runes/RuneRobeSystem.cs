// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Wizard.Runes;

[RegisterComponent, NetworkedComponent]
public sealed partial class RuneRobeComponent : Component
{
    [DataField(required: true)]
    public ProtoId<DamageModifierSetPrototype> Modifiers;
}

[RegisterComponent]
public sealed partial class RuneScrollHatComponent : Component
{
    [DataField]
    public EntProtoId Scroll = "WizardRuneScroll";

    [DataField]
    public float Interval = 120f;

    public EntityUid? Wearer;

    public TimeSpan Next;
}

public sealed class RuneRobeSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RuneRobeComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnDamageModify);
        SubscribeLocalEvent<RuneRobeComponent, ExaminedEvent>(OnExamined);
    }

    private void OnDamageModify(Entity<RuneRobeComponent> ent, ref InventoryRelayedEvent<DamageModifyEvent> args)
    {
        if (!HasComp<RuneCasterComponent>(args.Owner) || !_proto.TryIndex(ent.Comp.Modifiers, out var modifiers))
            return;

        if (TryComp<LimbBodyComponent>(args.Owner, out var body) && body.ArmorZone is { } zone && !LimbCoverRules.Covers(ent, zone, EntityManager))
            return;

        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, modifiers);
    }

    private void OnExamined(Entity<RuneRobeComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("rune-robe-examine"));
    }
}
