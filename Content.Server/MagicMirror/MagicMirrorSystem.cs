using System.Linq;
using Content.Server.DoAfter;
using Content.Server.Humanoid;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.MagicMirror;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.MagicMirror;

/// <summary>
/// Allows humanoids to change their appearance mid-round.
/// </summary>
public sealed partial class MagicMirrorSystem : SharedMagicMirrorSystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DoAfterSystem _doAfterSystem = default!;
    [Dependency] private MarkingManager _markings = default!;
    [Dependency] private HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private TagSystem _tagSystem = default!;

    private static readonly ProtoId<TagPrototype> HidesHairTag = "HidesHair";

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<MagicMirrorComponent>(MagicMirrorUiKey.Key,
            subs =>
        {
            subs.Event<BoundUIClosedEvent>(OnUiClosed);
            subs.Event<MagicMirrorSelectMessage>(OnMagicMirrorSelect);
            subs.Event<MagicMirrorChangeColorMessage>(OnTryMagicMirrorChangeColor);
            subs.Event<MagicMirrorAddSlotMessage>(OnTryMagicMirrorAddSlot);
            subs.Event<MagicMirrorRemoveSlotMessage>(OnTryMagicMirrorRemoveSlot);
            subs.Event<MagicMirrorChangeGradientMessage>(OnTryMagicMirrorChangeGradient); // DS14
        });


        SubscribeLocalEvent<MagicMirrorComponent, MagicMirrorSelectDoAfterEvent>(OnSelectSlotDoAfter);
        SubscribeLocalEvent<MagicMirrorComponent, MagicMirrorChangeColorDoAfterEvent>(OnChangeColorDoAfter);
        SubscribeLocalEvent<MagicMirrorComponent, MagicMirrorRemoveSlotDoAfterEvent>(OnRemoveSlotDoAfter);
        SubscribeLocalEvent<MagicMirrorComponent, MagicMirrorAddSlotDoAfterEvent>(OnAddSlotDoAfter);
        SubscribeLocalEvent<MagicMirrorComponent, MagicMirrorChangeGradientDoAfterEvent>(OnChangeGradientDoAfter); // DS14
    }

    private void OnMagicMirrorSelect(EntityUid uid, MagicMirrorComponent component, MagicMirrorSelectMessage message)
    {
        if (component.Target is not { } target)
            return;

        // Check if the target getting their hair altered has any clothes that hides their hair
        if (CheckHeadSlotOrClothes(message.Actor, component.Target.Value))
        {
            _popup.PopupEntity(
                component.Target == message.Actor
                    ? Loc.GetString("magic-mirror-blocked-by-hat-self")
                    : Loc.GetString("magic-mirror-blocked-by-hat-self-target", ("target", Identity.Entity(message.Actor, EntityManager))),
                message.Actor,
                message.Actor,
                PopupType.Medium);
            return;
        }

        _doAfterSystem.Cancel(component.DoAfter);
        component.DoAfter = null;

        var doafterTime = component.SelectSlotTime;
        if (component.Target == message.Actor)
            doafterTime /= 3;

        var doAfter = new MagicMirrorSelectDoAfterEvent()
        {
            Category = message.Category,
            Slot = message.Slot,
            Marking = message.Marking,
        };

        _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, message.Actor, doafterTime, doAfter, uid, target: target, used: uid)
        {
            DistanceThreshold = SharedInteractionSystem.InteractionRange,
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
        },
            out var doAfterId);

        if (component.Target == message.Actor)
        {
            _popup.PopupEntity(Loc.GetString("magic-mirror-change-slot-self"), component.Target.Value, component.Target.Value, PopupType.Medium);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("magic-mirror-change-slot-target", ("user", Identity.Entity(message.Actor, EntityManager))), component.Target.Value, component.Target.Value, PopupType.Medium);
        }

        component.DoAfter = doAfterId;
        _audio.PlayPvs(component.ChangeHairSound, uid);
    }

    private void OnSelectSlotDoAfter(EntityUid uid, MagicMirrorComponent component, MagicMirrorSelectDoAfterEvent args)
    {
        component.DoAfter = null;

        if (args.Handled || args.Target == null || args.Cancelled)
            return;

        if (component.Target != args.Target)
            return;

        MarkingCategories category;
        var markingId = args.Marking; // DS14

        switch (args.Category)
        {
            case MagicMirrorCategory.Hair:
                category = MarkingCategories.Hair;
                // DS14-start
                if (TryComp<HumanoidAppearanceComponent>(component.Target.Value, out var humanoid)
                    && humanoid.HairGradientEnabled)
                {
                    var gradientId = markingId + "Gradient";
                    if (_markings.Markings.ContainsKey(gradientId))
                        markingId = gradientId;
                }
                // DS14-end
                break;
            case MagicMirrorCategory.FacialHair:
                category = MarkingCategories.FacialHair;
                break;
            default:
                return;
        }

        _humanoid.SetMarkingId(component.Target.Value, category, args.Slot, markingId);

        // DS14-start
        if (args.Category == MagicMirrorCategory.Hair
            && TryComp<HumanoidAppearanceComponent>(component.Target.Value, out var h)
            && h.HairGradientEnabled && _markings.Markings.TryGetValue(markingId, out var proto)
            && proto.Sprites.Count > 1)
        {
            var colors = new List<Color>(2) { h.HairGradientColor, h.HairGradientColor };
            _humanoid.SetMarkingColor(component.Target.Value, MarkingCategories.Hair, args.Slot, colors);
        }
        // DS14-end

        UpdateInterface(uid, component.Target.Value, component);
    }

    private void OnTryMagicMirrorChangeColor(EntityUid uid, MagicMirrorComponent component, MagicMirrorChangeColorMessage message)
    {
        if (component.Target is not { } target)
            return;

        // Check if the target getting their hair altered has any clothes that hides their hair
        if (CheckHeadSlotOrClothes(message.Actor, component.Target.Value))
        {
            _popup.PopupEntity(
                component.Target == message.Actor
                    ? Loc.GetString("magic-mirror-blocked-by-hat-self")
                    : Loc.GetString("magic-mirror-blocked-by-hat-self-target", ("target", Identity.Entity(message.Actor, EntityManager))),
                message.Actor,
                message.Actor,
                PopupType.Medium);
            return;
        }

        _doAfterSystem.Cancel(component.DoAfter);
        component.DoAfter = null;

        var doafterTime = component.ChangeSlotTime;
        if (component.Target == message.Actor)
            doafterTime /= 3;

        var doAfter = new MagicMirrorChangeColorDoAfterEvent()
        {
            Category = message.Category,
            Slot = message.Slot,
            Colors = message.Colors,
        };

        _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, message.Actor, doafterTime, doAfter, uid, target: target, used: uid)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true
        },
            out var doAfterId);

        if (component.Target == message.Actor)
        {
            _popup.PopupEntity(Loc.GetString("magic-mirror-change-color-self"), component.Target.Value, component.Target.Value, PopupType.Medium);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("magic-mirror-change-color-target", ("user", Identity.Entity(message.Actor, EntityManager))), component.Target.Value, component.Target.Value, PopupType.Medium);
        }

        component.DoAfter = doAfterId;
    }
    private void OnChangeColorDoAfter(EntityUid uid, MagicMirrorComponent component, MagicMirrorChangeColorDoAfterEvent args)
    {
        component.DoAfter = null;

        if (args.Handled || args.Target == null || args.Cancelled)
            return;

        if (component.Target != args.Target)
            return;

        MarkingCategories category;
        switch (args.Category)
        {
            case MagicMirrorCategory.Hair:
                category = MarkingCategories.Hair;
                break;
            case MagicMirrorCategory.FacialHair:
                category = MarkingCategories.FacialHair;
                break;
            default:
                return;
        }

        _humanoid.SetMarkingColor(component.Target.Value, category, args.Slot, args.Colors);

        // using this makes the UI feel like total ass
        // que
        // UpdateInterface(uid, component.Target, message.Session);
    }

    private void OnTryMagicMirrorRemoveSlot(EntityUid uid, MagicMirrorComponent component, MagicMirrorRemoveSlotMessage message)
    {
        if (component.Target is not { } target)
            return;

        // Check if the target getting their hair altered has any clothes that hides their hair
        if (CheckHeadSlotOrClothes(message.Actor, component.Target.Value))
        {
            _popup.PopupEntity(
                component.Target == message.Actor
                    ? Loc.GetString("magic-mirror-blocked-by-hat-self")
                    : Loc.GetString("magic-mirror-blocked-by-hat-self-target", ("target", Identity.Entity(message.Actor, EntityManager))),
                message.Actor,
                message.Actor,
                PopupType.Medium);
            return;
        }

        _doAfterSystem.Cancel(component.DoAfter);
        component.DoAfter = null;

        var doafterTime = component.RemoveSlotTime;
        if (component.Target == message.Actor)
            doafterTime /= 3;

        var doAfter = new MagicMirrorRemoveSlotDoAfterEvent()
        {
            Category = message.Category,
            Slot = message.Slot,
        };

        _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, message.Actor, doafterTime, doAfter, uid, target: target, used: uid)
        {
            DistanceThreshold = SharedInteractionSystem.InteractionRange,
            BreakOnDamage = true,
            NeedHand = true
        },
            out var doAfterId);

        if (component.Target == message.Actor)
        {
            _popup.PopupEntity(Loc.GetString("magic-mirror-remove-slot-self"), component.Target.Value, component.Target.Value, PopupType.Medium);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("magic-mirror-remove-slot-target", ("user", Identity.Entity(message.Actor, EntityManager))), component.Target.Value, component.Target.Value, PopupType.Medium);
        }

        component.DoAfter = doAfterId;
        _audio.PlayPvs(component.ChangeHairSound, uid);
    }

    private void OnRemoveSlotDoAfter(EntityUid uid, MagicMirrorComponent component, MagicMirrorRemoveSlotDoAfterEvent args)
    {
        component.DoAfter = null;

        if (args.Handled || args.Target == null || args.Cancelled)
            return;

        if (component.Target != args.Target)
            return;

        MarkingCategories category;

        switch (args.Category)
        {
            case MagicMirrorCategory.Hair:
                category = MarkingCategories.Hair;
                break;
            case MagicMirrorCategory.FacialHair:
                category = MarkingCategories.FacialHair;
                break;
            default:
                return;
        }

        _humanoid.RemoveMarking(component.Target.Value, category, args.Slot);

        UpdateInterface(uid, component.Target.Value, component);
    }

    private void OnTryMagicMirrorAddSlot(EntityUid uid, MagicMirrorComponent component, MagicMirrorAddSlotMessage message)
    {
        if (component.Target == null)
            return;

        // Check if the target getting their hair altered has any clothes that hides their hair
        if (CheckHeadSlotOrClothes(message.Actor, component.Target.Value))
        {
            _popup.PopupEntity(
                component.Target == message.Actor
                    ? Loc.GetString("magic-mirror-blocked-by-hat-self")
                    : Loc.GetString("magic-mirror-blocked-by-hat-self-target", ("target", Identity.Entity(message.Actor, EntityManager))),
                message.Actor,
                message.Actor,
                PopupType.Medium);
            return;
        }

        _doAfterSystem.Cancel(component.DoAfter);
        component.DoAfter = null;

        var doafterTime = component.AddSlotTime;
        if (component.Target == message.Actor)
            doafterTime /= 3;

        var doAfter = new MagicMirrorAddSlotDoAfterEvent()
        {
            Category = message.Category,
        };

        _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, message.Actor, doafterTime, doAfter, uid, target: component.Target.Value, used: uid)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
        },
            out var doAfterId);

        if (component.Target == message.Actor)
        {
            _popup.PopupEntity(Loc.GetString("magic-mirror-add-slot-self"), component.Target.Value, component.Target.Value, PopupType.Medium);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("magic-mirror-add-slot-target", ("user", Identity.Entity(message.Actor, EntityManager))), component.Target.Value, component.Target.Value, PopupType.Medium);
        }

        component.DoAfter = doAfterId;
        _audio.PlayPvs(component.ChangeHairSound, uid);
    }
    private void OnAddSlotDoAfter(EntityUid uid, MagicMirrorComponent component, MagicMirrorAddSlotDoAfterEvent args)
    {
        component.DoAfter = null;

        if (args.Handled || args.Target == null || args.Cancelled || !TryComp(component.Target, out HumanoidAppearanceComponent? humanoid))
            return;

        MarkingCategories category;
        var markingId = string.Empty; // DS14

        switch (args.Category)
        {
            case MagicMirrorCategory.Hair:
                category = MarkingCategories.Hair;
                // DS14-start
                markingId = _markings.MarkingsByCategoryAndSpecies(category, humanoid.Species).Keys.FirstOrDefault();
                if (!string.IsNullOrEmpty(markingId) && humanoid.HairGradientEnabled)
                {
                    var gradientId = markingId + "Gradient";
                    if (_markings.Markings.ContainsKey(gradientId))
                        markingId = gradientId;
                }
                // DS14-end
                break;
            case MagicMirrorCategory.FacialHair:
                category = MarkingCategories.FacialHair;
                markingId = _markings.MarkingsByCategoryAndSpecies(category, humanoid.Species).Keys.FirstOrDefault(); // DS14
                break;
            default:
                return;
        }

        if (string.IsNullOrEmpty(markingId)) // DS14
            return;

        _humanoid.AddMarking(component.Target.Value, markingId, Color.Black); // DS14

        UpdateInterface(uid, component.Target.Value, component);

    }

    // DS14-start
    private void OnTryMagicMirrorChangeGradient(EntityUid uid, MagicMirrorComponent component, MagicMirrorChangeGradientMessage message)
    {
        if (component.Target is not { } target)
            return;

        if (CheckHeadSlotOrClothes(message.Actor, component.Target.Value))
        {
            _popup.PopupEntity(
                component.Target == message.Actor
                    ? Loc.GetString("magic-mirror-blocked-by-hat-self")
                    : Loc.GetString("magic-mirror-blocked-by-hat-self-target", ("target", Identity.Entity(message.Actor, EntityManager))),
                message.Actor,
                message.Actor,
                PopupType.Medium);
            return;
        }

        _doAfterSystem.Cancel(component.DoAfter);
        component.DoAfter = null;

        var doafterTime = component.ChangeSlotTime;
        if (component.Target == message.Actor)
            doafterTime /= 3;

        var doAfter = new MagicMirrorChangeGradientDoAfterEvent()
        {
            Enabled = message.Enabled,
            Color = message.Color,
        };

        _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, message.Actor, doafterTime, doAfter, uid, target: target, used: uid)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
        },
            out var doAfterId);

        component.DoAfter = doAfterId;
    }

    private void OnChangeGradientDoAfter(EntityUid uid, MagicMirrorComponent component, MagicMirrorChangeGradientDoAfterEvent args)
    {
        component.DoAfter = null;

        if (args.Handled || args.Target == null || args.Cancelled)
            return;

        if (component.Target != args.Target)
            return;

        if (!TryComp<HumanoidAppearanceComponent>(component.Target.Value, out var humanoid))
            return;

        humanoid.HairGradientEnabled = args.Enabled;
        humanoid.HairGradientColor = args.Color;

        if (humanoid.MarkingSet.TryGetCategory(MarkingCategories.Hair, out var hairMarkings) && hairMarkings.Count > 0)
        {
            var currentId = hairMarkings[0].MarkingId;
            var baseColor = hairMarkings[0].MarkingColors.Count > 0 ? hairMarkings[0].MarkingColors[0] : Color.White;

            if (args.Enabled)
            {
                var baseId = currentId.EndsWith("Gradient") ? currentId[..^"Gradient".Length] : currentId;
                var gradientId = baseId + "Gradient";
                if (_markings.Markings.ContainsKey(gradientId))
                {
                    var colors = new List<Color> { baseColor, args.Color };
                    _humanoid.SetMarkingId(component.Target.Value, MarkingCategories.Hair, 0, gradientId);
                    _humanoid.SetMarkingColor(component.Target.Value, MarkingCategories.Hair, 0, colors);
                }
            }
            else
            {
                if (currentId.EndsWith("Gradient"))
                {
                    var baseId = currentId[..^"Gradient".Length];
                    if (_markings.Markings.ContainsKey(baseId))
                    {
                        var colors = new List<Color> { baseColor };
                        _humanoid.SetMarkingId(component.Target.Value, MarkingCategories.Hair, 0, baseId);
                        _humanoid.SetMarkingColor(component.Target.Value, MarkingCategories.Hair, 0, colors);
                    }
                }
            }
        }

        Dirty(component.Target.Value, humanoid);
        UpdateInterface(uid, component.Target.Value, component);
    }
    // DS14-end

    private void OnUiClosed(Entity<MagicMirrorComponent> ent, ref BoundUIClosedEvent args)
    {
        ent.Comp.Target = null;
        Dirty(ent);
    }

    /// <summary>
    /// Helper function that checks if the wearer has anything on their head
    /// Or if they have any clothes that hides their hair
    /// </summary>
    private bool CheckHeadSlotOrClothes(EntityUid user, EntityUid target)
    {
        if (TryComp<InventoryComponent>(target, out var inventoryComp))
        {
            // any hat whatsoever will block haircutting
            if (_inventory.TryGetSlotEntity(target, "head", out var hat, inventoryComp))
            {
                return true;
            }

            // maybe there's some kind of armor that has the HidesHair tag as well, so check every slot for it
            var slots = _inventory.GetSlotEnumerator((target, inventoryComp), SlotFlags.WITHOUT_POCKET);
            while (slots.MoveNext(out var slot))
            {
                if (slot.ContainedEntity != null && _tagSystem.HasTag(slot.ContainedEntity.Value, HidesHairTag))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
