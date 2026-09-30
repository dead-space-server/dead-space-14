using Content.Shared.Actions;
using Content.Shared.Light.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.Toggleable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Inventory;
using Content.Shared.Administration.Logs;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Content.Shared.Nutrition;
using System.Linq;

namespace Content.Server.Light.EntitySystems
{
    public sealed class EbalSystem : EntitySystem
    {
        [Dependency] private readonly OpenableSystem _openable = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly SharedActionsSystem _actions = default!;

        [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
        [Dependency] private readonly IngestionSystem ss = default!;
        [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly InventorySystem _inventorySystem = default!;
        [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
        [Dependency] private readonly IPrototypeManager _proto = default!;
        [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
        [Dependency] private readonly FlavorProfileSystem _flavorProfile = default!;
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

            SubscribeLocalEvent<EbalComponent, EdibleEvent>(S);
            SubscribeLocalEvent<EbalComponent, BeforeIngestedEvent>(Y);
            SubscribeLocalEvent<EbalComponent, IngestedEvent>(OnEdibleIngested);
            SubscribeLocalEvent<EbalComponent, IsDigestibleEvent>(OnDrainableIsDigestible);
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

            args.Handled = ss.TryIngest(args.Performer, ent);
        }

        private void OnDrainableIsDigestible(Entity<EbalComponent> ent, ref IsDigestibleEvent args)
        {
            args.UniversalDigestion();
        }
        private void OnEdibleIngested(Entity<EbalComponent> entity, ref IngestedEvent args)
        {
            // This is a lot but there wasn't really a way to separate this from the EdibleComponent otherwise I would've moved it.

            if (args.Handled)
                return;

            args.Handled = true;

            var edible = _proto.Index(entity.Comp.Edible);
            _audio.PlayPredicted(entity.Comp.UseSound, args.Target, args.User);

            var flavors = _flavorProfile.GetLocalizedFlavorsMessage(entity.Owner, args.Target, args.Split);

            _popup.PopupPredicted(Loc.GetString(edible.Message, ("food", entity.Owner), ("flavors", flavors)),
                Loc.GetString(edible.OtherMessage),
                args.User,
                args.User);

            // log successful voluntary eating
            // TODO: Use correct verb
            // the past tense is tricky here
            // localized admin logs when?
            _adminLogger.Add(LogType.Ingestion, LogImpact.Low, $"{ToPrettyString(args.User):target} ate {ToPrettyString(entity):food}");


            // This also prevents us from repeating if it's empty
            if (!IsEmpty(entity))
            {
                args.Repeat = true;
            }
            else
            {
                args.Repeat = false;
            }

            args.Destroy = false;
        }

        private bool IsEmpty(Entity<EbalComponent> entity)
        {
            var slots = Comp<ItemSlotsComponent>(entity).Slots.Values;

            if (slots.Any(slot => slot.Item != null && !_openable.IsClosed(slot.Item.Value) &&
            _solutionContainer.TryGetSolution(slot.Item.Value, "drink", out var solution)
            && solution.Value.Comp.Solution.Volume != FixedPoint2.Zero))
            {
                return false;
            }


            return true;
        }
        private void Y(Entity<EbalComponent> entity, ref BeforeIngestedEvent args)
        {
            if (args.Cancelled || args.Solution == null)
                return;

            if (IsEmpty(entity))
            {
                args.Cancelled = true;
                return;
            }
            // Set it to transfer amount if it exists, otherwise eat the whole volume if possible.
            args.Transfer = entity.Comp.TransferAmount ?? args.Solution.Volume;

            if (!_solutionContainer.TryGetSolution(entity.Owner, "drink", out var ToSolution))
                return;

            var slots = Comp<ItemSlotsComponent>(entity).Slots.Values.Where(slot =>
                slot.Item != null && !_openable.IsClosed(slot.Item.Value)
                && _solutionContainer.TryGetSolution(slot.Item.Value, "drink", out var fromSolution)
                && fromSolution.Value.Comp.Solution.Volume != FixedPoint2.Zero);

            foreach (var slot in slots)
            {
                var item = slot.Item;
                if (item != null && _solutionContainer.TryGetSolution(item.Value, "drink", out var fromSolution))
                {
                    var split = _solutionContainer.SplitSolution(fromSolution.Value, args.Transfer / slots.Count());

                    _solutionContainer.TryAddSolution(ToSolution.Value, split);
                }
            }
        }

        private void S(Entity<EbalComponent> entity, ref EdibleEvent args)
        {
            if (args.Cancelled || args.Solution != null)
                return;

            var parent = Transform(entity).ParentUid;
            if (parent != args.User || !_inventorySystem.InSlotWithFlags(entity.Owner, entity.Comp.RequiredFlags))
            {
                args.Cancelled = true;
                return;
            }

            // Check this last
            if (!_solutionContainer.TryGetSolution(entity.Owner, entity.Comp.Solution, out args.Solution) || IsEmpty(entity))
            {
                args.Cancelled = true;

                _popup.PopupClient(Loc.GetString("ingestion-try-use-is-empty", ("entity", entity)), entity, args.User);
                return;
            }

            // Time is additive because I said so.
            args.Time += TimeSpan.FromSeconds(1);
        }
        private void OnMapInit(Entity<EbalComponent> ent, ref MapInitEvent args)
        {
            var component = ent.Comp;
            _actionContainer.EnsureAction(ent, ref component.ToggleActionEntity, component.ToggleAction);
        }

        private void OnShutdown(EntityUid uid, EbalComponent component, ComponentShutdown args)
        {
            if (component.s != null)
            {
                _doAfter.Cancel(component.s);
                component.s = null;
            }
            _actions.RemoveAction(uid, component.ToggleActionEntity);
        }
    }
}
