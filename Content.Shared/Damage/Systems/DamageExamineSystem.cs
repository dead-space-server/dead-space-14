using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Damage.Systems;

public sealed partial class DamageExamineSystem : EntitySystem
{
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DamageExaminableComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
    }

    private void OnGetExamineVerbs(EntityUid uid, DamageExaminableComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var ev = new DamageExamineEvent(new FormattedMessage(), args.User);
        RaiseLocalEvent(uid, ref ev);
        if (!ev.Message.IsEmpty)
        {
            _examine.AddDetailedExamineVerb(args, component, ev.Message,
                Loc.GetString("damage-examinable-verb-text"),
                "/Textures/Interface/VerbIcons/smite.svg.192dpi.png",
                Loc.GetString("damage-examinable-verb-message")
            );
        }
    }

    public void AddDamageExamine(FormattedMessage message, DamageSpecifier damageSpecifier, string? type = null, float? stamina = null)
    {
        var markup = GetDamageExamine(damageSpecifier, type, stamina);
        if (!message.IsEmpty)
        {
            message.PushNewline();
        }
        message.AddMessage(markup);
    }

    /// <summary>
    /// Retrieves the damage examine values.
    /// </summary>
    private FormattedMessage GetDamageExamine(DamageSpecifier damageSpecifier, string? type = null, float? stamina = null)
    {
        var msg = new FormattedMessage();

        if (string.IsNullOrEmpty(type))
        {
            msg.AddMarkupOrThrow(Loc.GetString("damage-examine"));
        }
        else
        {
            if (damageSpecifier.Empty &&
                !damageSpecifier.AnyPositive() &&
                stamina is null)
            {
                msg.AddMarkupOrThrow(Loc.GetString("damage-none"));
                return msg;
            }

            msg.AddMarkupOrThrow(Loc.GetString("damage-examine-type", ("type", type)));
        }

        foreach (var damage in damageSpecifier.DamageDict)
        {
            if (damage.Value != FixedPoint2.Zero)
            {
                msg.PushNewline();
                msg.AddMarkupOrThrow(Loc.GetString("damage-value", ("type", ProtoMan.Index<DamageTypePrototype>(damage.Key).LocalizedName), ("amount", damage.Value)));
            }
        }

<<<<<<< ours
        // DS14-Start
        var anyPiercing = false;

        foreach (var damage in damageSpecifier.DamageDict)
        {
            var piercing = damageSpecifier.GetArmorPiercingLevelForType(damage.Key);

            if (piercing <= 1)
                continue;

            if (!anyPiercing)
            {
                msg.PushNewline();
                msg.AddMarkupOrThrow(
                    Loc.GetString(
                        "damage-examine-piercing-lvl",
                        ("type", type ?? Loc.GetString("damage-examine"))
                    ));
                anyPiercing = true;
            }

            msg.PushNewline();
            msg.AddMarkupOrThrow(
                Loc.GetString(
                    "damage-piercing-lvl-value",
                    ("amount", piercing),
                    ("type", _prototype.Index<DamageTypePrototype>(damage.Key).LocalizedName)
                ));
        }
        // DS14-End

||||||| base
=======
        if (stamina is not null)
        {
            msg.PushNewline();
            msg.AddMarkupOrThrow(Loc.GetString("damage-value-stamina", ("amount", stamina * _stamina.UniversalStaminaDamageModifier)));
        }

>>>>>>> theirs
        return msg;
    }
}
