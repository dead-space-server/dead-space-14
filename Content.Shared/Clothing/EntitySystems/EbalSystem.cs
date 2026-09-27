using Content.Shared.Actions;
using Content.Shared.Light.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.Toggleable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Components;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.Containers.ItemSlots;
using System.Linq;

namespace Content.Server.Light.EntitySystems
{
    public sealed class EbalSystem : EntitySystem
    {
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly SharedActionsSystem _actions = default!;

        [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
        [Dependency] private readonly IngestionSystem ss = default!;
        [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
        [Dependency] private readonly PowerCellSystem _powerCell = default!;
        [Dependency] private readonly SharedBatterySystem _battery = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly SharedPointLightSystem _lights = default!;

        // TODO: Ideally you'd be able to subscribe to power stuff to get events at certain percentages.. or something?
        // But for now this will be better anyway

        public DoAfterId? g = null;

        public override void Initialize()
        {
            base.Initialize();


            SubscribeLocalEvent<EbalComponent, MapInitEvent>(OnMapInit);
            SubscribeLocalEvent<EbalComponent, ComponentShutdown>(OnShutdown);

            SubscribeLocalEvent<EbalComponent, GetItemActionsEvent>(OnGetActions);
            SubscribeLocalEvent<EbalComponent, ToggleActionEvent>(OnToggleAction);

            SubscribeLocalEvent<EbalComponent, EntRemovedFromContainerMessage>(OnEntInserted);
        }

        private void OnEntInserted(Entity<EbalComponent> ent, ref EntRemovedFromContainerMessage args)
        {
            if (ent.Comp.s != null)
            {
                _doAfter.Cancel(ent.Comp.s);
                ent.Comp.s = null;
            }
        }

        private void OnGetActions(EntityUid uid, EbalComponent component, GetItemActionsEvent args)
        {
            if ((args.SlotFlags & component.RequiredFlags) == component.RequiredFlags)
                args.AddAction(ref component.ToggleActionEntity, component.ToggleAction);
        }

        private void OnToggleAction(Entity<EbalComponent> ent, ref ToggleActionEvent args)
        {
            if (args.Handled)
                return;

            var a = Comp<ItemSlotsComponent>(ent);

            if (a.Slots.Count != 0)
            {
                var b = a.Slots.First().Value.Item;
                if (b != null)
                {
                    if (Comp<OpenableComponent>(b.Value).Opened)
                    {
                        var d = ss.GetEdibleDoAfterArgs(args.Performer, args.Performer, b.Value, TimeSpan.FromSeconds(1));
                        d.DistanceThreshold = null;
                        if (!_doAfter.TryStartDoAfter(d))
                            return;
                        ent.Comp.s = d.Event.DoAfter.Id;
                    }
                    else
                        _popup.PopupEntity("sss", ent, PopupType.Medium);
                }
            }
            else
            {
                _popup.PopupEntity("ПУСТО", ent, PopupType.Medium);
            }
            args.Handled = true;
        }
        private void OnMapInit(Entity<EbalComponent> ent, ref MapInitEvent args)
        {
            var component = ent.Comp;
            _actionContainer.EnsureAction(ent, ref component.ToggleActionEntity, component.ToggleAction);
            _actions.AddAction(ent, ref component.SelfToggleActionEntity, component.ToggleAction);
        }

        private void OnShutdown(EntityUid uid, EbalComponent component, ComponentShutdown args)
        {
            if (component.s != null)
            {
                _doAfter.Cancel(component.s);
                component.s = null;
            }
            _actions.RemoveAction(uid, component.ToggleActionEntity);
            _actions.RemoveAction(uid, component.SelfToggleActionEntity);
        }
    }
}
