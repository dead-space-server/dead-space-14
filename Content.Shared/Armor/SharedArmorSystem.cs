using Content.Shared.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.DeadSpace.Body.Limbs; // DS14
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared.Armor;

/// <summary>
///     This handles logic relating to <see cref="ArmorComponent" />
/// </summary>
public abstract class SharedArmorSystem : EntitySystem
{
    [Dependency] private readonly ExamineSystemShared _examine = default!;

    /// <inheritdoc />
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArmorComponent, InventoryRelayedEvent<CoefficientQueryEvent>>(OnCoefficientQuery);
        SubscribeLocalEvent<ArmorComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnDamageModify);
        SubscribeLocalEvent<ArmorComponent, BorgModuleRelayedEvent<DamageModifyEvent>>(OnBorgDamageModify);
        SubscribeLocalEvent<ArmorComponent, GetVerbsEvent<ExamineVerb>>(OnArmorVerbExamine);
    }

    // DS14-Start
    /// <summary>
    ///     Overrides the armor modifiers on an item. Used for runtime equipment (e.g. arena vests).
    /// </summary>
    public void SetModifiers(EntityUid uid, DamageModifierSet modifiers, ArmorComponent? armor = null)
    {
        if (!Resolve(uid, ref armor))
            return;

        armor.Modifiers = modifiers;
        Dirty(uid, armor);
    }
    // DS14-End

    /// <summary>
    /// Get the total Damage reduction value of all equipment caught by the relay.
    /// </summary>
    /// <param name="ent">The item that's being relayed to</param>
    /// <param name="args">The event, contains the running count of armor percentage as a coefficient</param>
    private void OnCoefficientQuery(Entity<ArmorComponent> ent, ref InventoryRelayedEvent<CoefficientQueryEvent> args)
    {
        if (TryComp<MaskComponent>(ent, out var mask) && mask.IsToggled)
            return;

        if (!CoversHit(ent, args.Owner)) // DS14
            return; // DS14

        foreach (var armorCoefficient in ent.Comp.Modifiers.Coefficients)
        {
            args.Args.DamageModifiers.Coefficients[armorCoefficient.Key] = args.Args.DamageModifiers.Coefficients.TryGetValue(armorCoefficient.Key, out var coefficient) ? coefficient * armorCoefficient.Value : armorCoefficient.Value;
        }
    }

    private void OnDamageModify(EntityUid uid, ArmorComponent component, InventoryRelayedEvent<DamageModifyEvent> args)
    {
        if (TryComp<MaskComponent>(uid, out var mask) && mask.IsToggled)
            return;

        if (component.Modifiers == null) // DS14
            return; // DS14

        if (!CoversHit(uid, args.Owner)) // DS14
            return; // DS14

        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, component.Modifiers);
    }

    // DS14-start
    private bool CoversHit(EntityUid clothing, EntityUid wearer)
    {
        if (!TryComp<LimbBodyComponent>(wearer, out var body) || body.ArmorZone is not { } zone)
            return true;

        return LimbCoverRules.Covers(clothing, zone, EntityManager);
    }
    // DS14-end

    private void OnBorgDamageModify(EntityUid uid, ArmorComponent component,
        ref BorgModuleRelayedEvent<DamageModifyEvent> args)
    {
        if (TryComp<MaskComponent>(uid, out var mask) && mask.IsToggled)
            return;

        if (component.Modifiers == null) // DS14
            return; // DS14

        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, component.Modifiers);
    }

    private void OnArmorVerbExamine(EntityUid uid, ArmorComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || !component.ShowArmorOnExamine)
            return;

        var examineMarkup = GetArmorExamine(uid, component.Modifiers); // DS14

        var ev = new ArmorExamineEvent(examineMarkup);
        RaiseLocalEvent(uid, ref ev);

        _examine.AddDetailedExamineVerb(args, component, examineMarkup,
            Loc.GetString("armor-examinable-verb-text"), "/Textures/Interface/VerbIcons/dot.svg.192dpi.png",
            Loc.GetString("armor-examinable-verb-message"));
    }

    private FormattedMessage GetArmorExamine(EntityUid uid, DamageModifierSet armorModifiers) // DS14
    {
        var msg = new FormattedMessage();

        // DS14-start
        var zones = new List<string>();
        foreach (var zone in LimbCoverRules.ExamineOrder)
        {
            if (!LimbCoverRules.Covers(uid, zone, EntityManager))
                continue;

            var key = ZoneKey(zone);
            if (key == null)
                continue;

            zones.Add(Loc.GetString("armor-zone-" + key));
        }

        if (zones.Count > 0)
        {
            msg.AddMarkupOrThrow(Loc.GetString("armor-examine-zones", ("zones", string.Join(", ", zones))));
            msg.PushNewline();
        }
        // DS14-end

        msg.AddMarkupOrThrow(Loc.GetString("armor-examine"));

        foreach (var coefficientArmor in armorModifiers.Coefficients)
        {
            msg.PushNewline();

            var armorType = Loc.GetString("armor-damage-type-" + coefficientArmor.Key.ToLower());
            msg.AddMarkupOrThrow(Loc.GetString("armor-coefficient-value",
                ("type", armorType),
                ("value", MathF.Round((1f - coefficientArmor.Value) * 100, 1))
            ));
        }

        foreach (var flatArmor in armorModifiers.FlatReduction)
        {
            msg.PushNewline();

            var armorType = Loc.GetString("armor-damage-type-" + flatArmor.Key.ToLower());
            msg.AddMarkupOrThrow(Loc.GetString("armor-reduction-value",
                ("type", armorType),
                ("value", flatArmor.Value)
            ));
        }

        // DS14-Start
        msg.PushNewline();
        msg.AddMarkupOrThrow(Loc.GetString("armor-lvl-examine"));
        foreach (var armorLvl in armorModifiers.ArmorLvls)
        {
            msg.PushNewline();

            var armorType = Loc.GetString("armor-damage-type-" + armorLvl.Key.ToLower());
            msg.AddMarkupOrThrow(Loc.GetString(
                "armor-lvl-value",
                ("type", armorType),
                ("value", armorLvl.Value)
            ));
        }
        // DS14-End

        return msg;
    }

    // DS14-start
    private static string? ZoneKey(LimbZone zone)
    {
        return zone switch
        {
            LimbZone.Head => "head",
            LimbZone.Eyes => "eyes",
            LimbZone.Mouth => "mouth",
            LimbZone.Torso => "torso",
            LimbZone.Groin => "groin",
            LimbZone.ArmLeft => "arm-left",
            LimbZone.ArmRight => "arm-right",
            LimbZone.HandLeft => "hand-left",
            LimbZone.HandRight => "hand-right",
            LimbZone.LegLeft => "leg-left",
            LimbZone.LegRight => "leg-right",
            LimbZone.FootLeft => "foot-left",
            LimbZone.FootRight => "foot-right",
            _ => null,
        };
    }
    // DS14-end
}
