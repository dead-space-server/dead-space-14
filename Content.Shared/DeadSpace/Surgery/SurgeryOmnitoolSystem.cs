// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;

namespace Content.Shared.DeadSpace.Surgery;

public sealed partial class SurgeryOmnitoolSystem : EntitySystem
{
    [Dependency] private ClothingSystem _clothing = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurgeryOmnitoolComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SurgeryHandBusyComponent, ContainerIsInsertingAttemptEvent>(OnBusyHandInsert);
        SubscribeLocalEvent<SurgeryOmnitoolComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<SurgeryOmnitoolComponent, GetVerbsEvent<EquipmentVerb>>(OnGetEquipmentVerbs);
    }

    private void OnMapInit(EntityUid uid, SurgeryOmnitoolComponent component, MapInitEvent args)
    {
        Apply(uid, component, component.Mode);
    }

    private void OnBusyHandInsert(Entity<SurgeryHandBusyComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Cancelled || ent.Comp.Hand == null || args.Container.ID != ent.Comp.Hand)
            return;

        if (Equipped(ent.Owner) != null)
            args.Cancel();
    }

    private void OnGetVerbs(EntityUid uid, SurgeryOmnitoolComponent component, GetVerbsEvent<Verb> args)
    {
        AddModeVerbs(uid, component, args.User, args.Verbs, Allowed(args), () => new Verb());
    }

    private void OnGetEquipmentVerbs(EntityUid uid, SurgeryOmnitoolComponent component, GetVerbsEvent<EquipmentVerb> args)
    {
        AddModeVerbs(uid, component, args.User, args.Verbs, Allowed(args), () => new EquipmentVerb());
    }

    private static bool Allowed<TVerb>(GetVerbsEvent<TVerb> args) where TVerb : Verb
    {
        return args.CanAccess && args.CanInteract && args.CanComplexInteract;
    }

    private void AddModeVerbs<TVerb>(EntityUid uid,
        SurgeryOmnitoolComponent component,
        EntityUid user,
        SortedSet<TVerb> verbs,
        bool allowed,
        Func<TVerb> factory)
        where TVerb : Verb
    {
        if (!allowed || component.Modes.Count < 2)
            return;

        var category = new VerbCategory(Loc.GetString("surgery-omnitool-verb-category"), null);

        for (var i = 0; i < component.Modes.Count; i++)
        {
            var index = i;
            var mode = component.Modes[i];

            var verb = factory();
            verb.Text = Loc.GetString(mode.Name);
            verb.Category = category;
            verb.Priority = 1;
            verb.Disabled = index == component.Mode;
            verb.DoContactInteraction = true;
            verb.Act = () => SetMode(uid, component, index, user);

            verbs.Add(verb);
        }
    }

    public EntityUid? Equipped(EntityUid user)
    {
        if (_inventory.TryGetSlotEntity(user, "gloves", out var gloves) && Is(gloves))
            return gloves;

        return null;
    }

    public bool Is(EntityUid? uid)
    {
        return uid is { } entity && HasComp<SurgeryOmnitoolComponent>(entity);
    }

    public void SetMode(EntityUid uid, SurgeryOmnitoolComponent component, int mode, EntityUid? user = null)
    {
        if (mode < 0 || mode >= component.Modes.Count || mode == component.Mode)
            return;

        Apply(uid, component, mode);
        component.Mode = mode;
        Dirty(uid, component);

        if (user == null)
            return;

        var text = Loc.GetString("surgery-omnitool-switched", ("mode", Loc.GetString(component.Modes[mode].Name)));
        _popup.PopupClient(text, user.Value, user);
    }

    private void Apply(EntityUid uid, SurgeryOmnitoolComponent component, int mode)
    {
        if (component.Modes.Count == 0)
            return;

        var index = Math.Clamp(mode, 0, component.Modes.Count - 1);
        _clothing.SetEquippedState(uid, component.Modes[index].EquippedState);
    }
}
