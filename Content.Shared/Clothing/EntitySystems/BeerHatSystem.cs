using Content.Shared.Actions;
using Robust.Shared.Audio.Systems;
using Content.Shared.Nutrition.EntitySystems;
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

namespace Content.Shared.Clothing.EntitySystems
{
    public sealed class BeerHatSystem : EntitySystem
    {
        [Dependency] private readonly OpenableSystem _openable = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly SharedActionsSystem _actions = default!;

        [Dependency] private readonly IngestionSystem _ingestionSystem = default!;
        [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly InventorySystem _inventorySystem = default!;
        [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
        [Dependency] private readonly IPrototypeManager _proto = default!;
        [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
        [Dependency] private readonly FlavorProfileSystem _flavorProfile = default!;

        public override void Initialize()
        {
            base.Initialize();


            SubscribeLocalEvent<BeerHatComponent, MapInitEvent>(OnMapInit);

            SubscribeLocalEvent<BeerHatComponent, ComponentShutdown>(OnShutdown);
            SubscribeLocalEvent<BeerHatComponent, GetItemActionsEvent>(OnGetActions);
            SubscribeLocalEvent<BeerHatComponent, DrinkFromBeerHatEvent>(OnToggleAction);

            SubscribeLocalEvent<BeerHatComponent, EdibleEvent>(OnEdible);
            SubscribeLocalEvent<BeerHatComponent, BeforeIngestedEvent>(OnBeforeIngested);
            SubscribeLocalEvent<BeerHatComponent, IngestedEvent>(OnEdibleIngested);
            SubscribeLocalEvent<BeerHatComponent, IsDigestibleEvent>(OnDrainableIsDigestible);
        }


        private void OnGetActions(EntityUid uid, BeerHatComponent component, GetItemActionsEvent args)
        {
            if ((args.SlotFlags & component.RequiredFlags) == component.RequiredFlags)
                args.AddAction(ref component.ActionEntity, component.Action);
        }

        private void OnToggleAction(Entity<BeerHatComponent> ent, ref DrinkFromBeerHatEvent args)
        {
            if (args.Handled)
                return;

            args.Handled = _ingestionSystem.TryIngest(args.Performer, ent);
        }

        private void OnDrainableIsDigestible(Entity<BeerHatComponent> ent, ref IsDigestibleEvent args)
        {
            args.UniversalDigestion();
        }
        private void OnEdible(Entity<BeerHatComponent> entity, ref EdibleEvent args)
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
            args.Time += entity.Comp.Delay;
        }

        private void OnBeforeIngested(Entity<BeerHatComponent> entity, ref BeforeIngestedEvent args)
        {
            if (args.Cancelled || args.Solution == null)
                return;

            args.Transfer = entity.Comp.TransferAmount ?? args.Solution.Volume;

            if (!_solutionContainer.TryGetSolution(entity.Owner, entity.Comp.Solution, out var toSolution) || !TryComp<ItemSlotsComponent>(entity, out var slots))
                return;

            //Боже храни хардкод
            var sortedSlots = slots.Slots.Values.Where(slot =>
                slot.Item != null && !_openable.IsClosed(slot.Item.Value)
                && _solutionContainer.TryGetSolution(slot.Item.Value, entity.Comp.Solution, out var fromSolution)
                && fromSolution.Value.Comp.Solution.Volume != FixedPoint2.Zero);

            foreach (var slot in sortedSlots)
            {
                var item = slot.Item;
                if (item != null && _solutionContainer.TryGetSolution(item.Value, entity.Comp.Solution, out var fromSolution))
                {
                    var split = _solutionContainer.SplitSolution(fromSolution.Value, args.Transfer / sortedSlots.Count());

                    _solutionContainer.TryAddSolution(toSolution.Value, split);
                }
            }
        }
        private void OnEdibleIngested(Entity<BeerHatComponent> entity, ref IngestedEvent args)
        {
            if (args.Handled)
                return;

            args.Handled = true;

            var edible = _proto.Index(entity.Comp.Edible);
            _audio.PlayPredicted(entity.Comp.UseSound ?? edible.UseSound, args.Target, args.User);

            var flavors = _flavorProfile.GetLocalizedFlavorsMessage(entity.Owner, args.Target, args.Split);

            _popup.PopupPredicted(Loc.GetString(edible.Message, ("food", entity.Owner), ("flavors", flavors)),
                Loc.GetString(edible.OtherMessage),
                args.User,
                args.User);


            _adminLogger.Add(LogType.Ingestion, LogImpact.Low, $"{ToPrettyString(args.User):target} ate {ToPrettyString(entity):food}");


            if (!IsEmpty(entity))
            {
                args.Repeat = true;
            }

            args.Destroy = false;
        }
        private void OnMapInit(Entity<BeerHatComponent> ent, ref MapInitEvent args)
        {
            var component = ent.Comp;
            _actionContainer.EnsureAction(ent, ref component.ActionEntity, component.Action);
        }

        private void OnShutdown(EntityUid uid, BeerHatComponent component, ComponentShutdown args)
        {
            _actions.RemoveAction(uid, component.ActionEntity);
        }
        private bool IsEmpty(Entity<BeerHatComponent> entity)
        {
            var slots = Comp<ItemSlotsComponent>(entity).Slots.Values;

            if (slots.Any(slot =>
            slot.Item != null && !_openable.IsClosed(slot.Item.Value) &&
            _solutionContainer.TryGetSolution(slot.Item.Value, entity.Comp.Solution, out var solution)
            && solution.Value.Comp.Solution.Volume != FixedPoint2.Zero))
            {
                return false;
            }


            return true;
        }
    }

    public sealed partial class DrinkFromBeerHatEvent : InstantActionEvent;
}
