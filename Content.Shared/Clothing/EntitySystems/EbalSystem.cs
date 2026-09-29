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
using Robust.Shared.Serialization;
using System.Linq;
using System.Linq;
using Content.Shared.Chemistry.Components;
using Content.Shared.Clothing;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Fluids.Components;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory;
using Content.Shared.Nutrition.Components;
using Content.Shared.Storage;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Administration.Logs;
using Content.Shared.Actions.Events;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Database;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Forensics.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Tools.EntitySystems;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Content.Shared.Nutrition;
using Content.Shared.Chemistry.Components.SolutionManager;

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

        [Dependency] private readonly SharedBodySystem _body = default!;
        [Dependency] private readonly ReactiveSystem _reaction = default!;
        [Dependency] private readonly StomachSystem _stomach = default!;

        [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
        
        [Dependency] private readonly IPrototypeManager _proto = default!;
        [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
        [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
        [Dependency] private readonly FlavorProfileSystem _flavorProfile = default!;
        [Dependency] private readonly MobStateSystem _mobState = default!;
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

            SubscribeLocalEvent<EbalComponent, EntRemovedFromContainerMessage>(OnEntInserted);
            // SubscribeLocalEvent<BodyComponent, MyEvent>(OnEatingDoAfter);
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
            args.Handled = true;
            if (IsEmpty(ent))
            {
                _popup.PopupClient(Loc.GetString("ingestion-try-use-is-empty", ("entity", ent)), ent, args.Performer);
                return;
            }
                
            var doAfterArgs = new DoAfterArgs(EntityManager, args.Performer, ent.Comp.Delay, new EatingDoAfterEvent(), args.Performer, ent, null) // DS14
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                MovementThreshold = 100f,
                DistanceThreshold = 0f,
                // do-after will stop if item is dropped when trying to feed someone else
                // or if the item started out in the user's own hands
                NeedHand = true, // DS14
            };
            _doAfter.TryStartDoAfter(doAfterArgs);
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

            foreach (var slot in slots)
            {
                if (slot.Item != null && _solutionContainer.TryGetSolution(slot.Item.Value, "drink", out var solution)
                && solution.Value.Comp.Solution.Volume != FixedPoint2.Zero)
                {
                    return false;
                }
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

            var ent = Comp<ItemSlotsComponent>(entity).Slots.First();
            if (!_solutionContainer.TryGetSolution(entity.Owner, "drink", out var ToSolution) ||
            ent.Value.Item == null || !_solutionContainer.TryGetSolution(ent.Value.Item.Value, "drink", out var FromSolution))
                return;


            var SOL = _solutionContainer.SplitSolution(FromSolution.Value, FixedPoint2.New(5));

            _solutionContainer.TryAddSolution(ToSolution.Value, SOL);

            if (_solutionContainer.TryGetSolution(entity.Owner, "drink", out var b))
            {
                args.Solution = b.Value.Comp.Solution;
            }
        }


        // public FixedPoint2 EdibleVolume(EntityUid entity)
        // {
        //     if (!_solutionContainer.TryGetSolution(entity, "drink", out _, out var solution))
        //         return FixedPoint2.Zero;

        //     return solution.Volume;
        // }

        // public bool IsEmpty(EntityUid entity)
        // {
        //     return EdibleVolume(entity) == FixedPoint2.Zero;
        // }
        private void S(Entity<EbalComponent> entity, ref EdibleEvent args)
        {
            if (args.Cancelled || args.Solution != null)
                return;

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

// private void OnEatingDoAfter(Entity<BodyComponent> entity, ref MyEvent args)
        // {
        //     if (args.Cancelled || args.Handled || entity.Comp.Deleted || args.Target == null)
        //         return;

        //     var food = args.Target.Value;

        //     var blockerEv = new IngestibleEvent();
        //     RaiseLocalEvent(food, ref blockerEv);

        //     if (blockerEv.Cancelled)
        //         return;

        //     if (!_solutionContainer.TryGetSolution(food, "drink", out var solution))
        //         return;

        //     if (!_body.TryGetBodyOrganEntityComps<StomachComponent>(entity!, out var stomachs))
        //         return;

        //     var highestAvailable = FixedPoint2.Zero;
        //     Entity<StomachComponent>? stomachToUse = null;
        //     foreach (var ent in stomachs)
        //     {
        //         var owner = ent.Owner;
        //         if (!_solutionContainer.ResolveSolution(owner, StomachSystem.DefaultSolutionName, ref ent.Comp1.Solution, out var stomachSol))
        //             continue;

        //         if (stomachSol.AvailableVolume <= highestAvailable)
        //             continue;

        //         if (!ss.IsDigestibleBy(food, ent))
        //             continue;

        //         stomachToUse = ent;
        //         highestAvailable = stomachSol.AvailableVolume;
        //     }

        //     // All stomachs are full or we have no stomachs
        //     if (stomachToUse == null)
        //     {
        //         // Very long
        //         _popup.PopupClient(Loc.GetString("ingestion-you-cannot-ingest-any-more", ("verb", ss.GetEdibleVerb(food))), entity, entity);
        //         return;
        //     }

        //     var beforeEv = new BeforeIngestedEvent(FixedPoint2.Zero, highestAvailable, solution.Value.Comp.Solution);
        //     RaiseLocalEvent(food, ref beforeEv);
        //     RaiseLocalEvent(entity, ref beforeEv);

        //     if (beforeEv.Cancelled || beforeEv.Min > beforeEv.Max)
        //     {
        //         // Very long x2
        //         _popup.PopupClient(Loc.GetString("ingestion-you-cannot-ingest-any-more", ("verb", ss.GetEdibleVerb(food))), entity, entity);
        //         return;
        //     }

        //     var transfer = FixedPoint2.Clamp(beforeEv.Transfer, beforeEv.Min, beforeEv.Max);

        //     var split = _solutionContainer.SplitSolution(solution.Value, transfer);

        //     if (beforeEv.Refresh)
        //         _solutionContainer.TryAddSolution(solution.Value, split);

        //     var ingestEv = new IngestingEvent(food, split, false);
        //     RaiseLocalEvent(entity, ref ingestEv);

        //     _reaction.DoEntityReaction(entity, split, ReactionMethod.Ingestion);

        //     // Everything is good to go item has been successfuly eaten
        //     var afterEv = new IngestedEvent(args.User, entity, split, false);
        //     RaiseLocalEvent(food, ref afterEv);

        //     _stomach.TryTransferSolution(stomachToUse.Value.Owner, split, stomachToUse);


        //     args.Repeat = afterEv.Repeat;
        // }

