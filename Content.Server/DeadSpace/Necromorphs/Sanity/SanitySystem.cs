// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Necromorphs.Sanity;
using Content.Shared.Popups;
using Content.Server.Mind;
using Content.Shared.Mind.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.NPC;
using Content.Server.Chat.Managers;
using Content.Shared.Ghost;
using Content.Server.GameTicking;
using Content.Server.Ghost;
using Content.Shared.NPC.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Robust.Shared.Prototypes;
using Content.Shared.StatusEffect;
using Content.Shared.Jittering;
using Content.Server.Speech.EntitySystems;
using Robust.Shared.Player;
using System.Linq;
using Content.Shared.DeadSpace.Necromorphs.Unitology.Components;
using Robust.Shared.Timing;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.Necromorphs.Sanity
{
    public sealed partial class SanitySystem : SharedSanitySystem
    {
        [Dependency] private IChatManager _chatMan = default!;
        [Dependency] private NPCSystem _npc = default!;
        [Dependency] private SharedPopupSystem _popup = default!;
        [Dependency] private MindSystem _mindSystem = default!;
        [Dependency] private GameTicker _gameTicker = default!;
        [Dependency] private GhostSystem _ghosts = default!;
        [Dependency] private SharedTransformSystem _transform = default!;
        [Dependency] private NpcFactionSystem _faction = default!;
        [Dependency] private SharedJitteringSystem _sharedJittering = default!;
        [Dependency] private SlurredSystem _slurred = default!;
        [Dependency] private ISharedPlayerManager _player = default!;
        [Dependency] private IGameTiming _time = default!;
        [Dependency] private IRobustRandom _random = default!;

        // private const string HighSanityMessage = "Вы чувствуете головную боль";
        // private const string MediumSanityMessage = "У вас болит голова, кости будто ломаются на части";
        // private const string LowSanityMessage = "Вы теряете рассудок, вам совсем плохо!";
        private const string LostSanityMessage = "Вы теряете сознание. Власть над вашим телом вам не принадлежит.";
        public static readonly ProtoId<StatusEffectPrototype> SlowedDownKey = "SlowedDown";
        public static readonly ProtoId<NpcFactionPrototype> SimpleHostileFaction = "SimpleHostile";
        public static readonly ProtoId<HTNCompoundPrototype> SimpleHostileCompound = "SimpleHostileCompound";

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<SanityComponent, SanityEvent>(OnSanity);
            SubscribeLocalEvent<SanityComponent, CheckCrazyMobEvent>(CheckCrazyMob);
            SubscribeLocalEvent<SanityComponent, ComponentShutdown>(OnSanityShutdown);
        }
        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var query = EntityQueryEnumerator<SanityComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                if (comp.NextCheckCrazyMob != TimeSpan.Zero && comp.NextCheckCrazyMob <= _time.CurTime)
                {
                    if (comp.SanityLevel <= 0)
                        Crazy(uid, comp);
                    else
                        comp.NextCheckCrazyMob = TimeSpan.Zero;
                }

                if (comp.NextCheckPopup == TimeSpan.Zero)
                    comp.NextCheckPopup = _time.CurTime + TimeSpan.FromSeconds(10);

                if (comp.NextCheckPopup > _time.CurTime) continue;
                if (_random.Prob(0.5f) && comp.SanityLevel < 60f)
                    _popup.PopupEntity(_random.Pick(comp.LowSanityMessages), uid, uid, PopupType.LargeCaution);

                comp.NextCheckPopup = _time.CurTime + TimeSpan.FromSeconds(10);
            }
        }
        private void OnSanityShutdown(EntityUid uid, SanityComponent comp, ComponentShutdown args)
        {
            if (!TryComp<GhostComponent>(comp.Ghost, out _))
                return;

            if (!_mindSystem.TryGetMind(uid, out var mindId, out var mind))
                return;

            _mindSystem.UnVisit(mindId, mind);

            UnCrazy(uid, comp);
        }

        private void OnSanity(EntityUid uid, SanityComponent comp, ref SanityEvent args)
        {
            if (comp.SanityLevel >= comp.MaxSanityLevel)
                return;

            var highSanityThreshold = comp.MaxSanityLevel * 0.66f;   // Верхние 33%
            var mediumSanityThreshold = comp.MaxSanityLevel * 0.33f; // Средние 33%
            var lowSanityThreshold = (float)0;                              // Нижние 33%

            switch (comp.SanityLevel)
            {
                case float sanityLevel when sanityLevel > highSanityThreshold:
                    // _popup.PopupEntity(HighSanityMessage, uid, uid);
                    HighSanity(uid, comp);
                    break;
                case float sanityLevel when sanityLevel <= highSanityThreshold && sanityLevel > mediumSanityThreshold:
                    // _popup.PopupEntity(MediumSanityMessage, uid, uid);
                    MediumSanity(uid, comp);
                    break;
                case float sanityLevel when sanityLevel <= mediumSanityThreshold && sanityLevel > lowSanityThreshold:
                    // _popup.PopupEntity(LowSanityMessage, uid, uid);
                    LowSanity(uid, comp);
                    break;
                case float sanityLevel when sanityLevel <= 0:
                    // _popup.PopupEntity(LostSanityMessage, uid, uid);
                    break;
                default:
                    break;
            }
        }

        private void LowSanity(EntityUid uid, SanityComponent comp)
        {
            MediumSanity(uid, comp);
            HighSanity(uid, comp);
            _slurred.DoSlur(uid, TimeSpan.FromSeconds(comp.UpdateDuration + 1));
        }

        private void MediumSanity(EntityUid uid, SanityComponent comp)
        {
            _sharedJittering.DoJitter(uid, TimeSpan.FromSeconds(comp.UpdateDuration + 1), true);
            HighSanity(uid, comp);
        }

        private void HighSanity(EntityUid uid, SanityComponent comp)
        {
            return;
        }

        private void CheckCrazyMob(EntityUid uid, SanityComponent comp, ref CheckCrazyMobEvent args)
        {
            if (comp.SanityLevel <= 0)
            {
                Crazy(uid, comp);
            }
            else
            {
                comp.NextCheckCrazyMob = TimeSpan.Zero;
                UnCrazy(uid, comp);
            }
        }

        private void Crazy(EntityUid uid, SanityComponent comp)
        {
            if (comp.IsCrazy)
                return;

            if (comp.NextCheckCrazyMob == TimeSpan.Zero)
            {
                _popup.PopupEntity(LostSanityMessage, uid, uid);
                comp.NextCheckCrazyMob = _time.CurTime + TimeSpan.FromSeconds(30);
                return;
            }

            if (comp.NextCheckCrazyMob > _time.CurTime)
                return;

            EnsureComp<UnitologyEnslavedComponent>(uid);
            comp.NextCheckCrazyMob = TimeSpan.Zero;

            if (TryComp<NpcFactionMemberComponent>(uid, out var factionComp))
                comp.OldFaction = factionComp.Factions.FirstOrDefault();

            if (TryComp<HTNComponent>(uid, out var hTNComponent))
                comp.OldTask = hTNComponent.RootTask.Task;

            var hasMind = _mindSystem.TryGetMind(uid, out var mindId, out var mind);

            RemComp<HTNComponent>(uid);
            var htn = EnsureComp<HTNComponent>(uid);
            htn.RootTask = new HTNCompoundTask() { Task = SimpleHostileCompound };
            htn.Blackboard.SetValue(NPCBlackboard.Owner, uid);
            _npc.WakeNPC(uid, htn);
            _faction.ClearFactions(uid, dirty: false);
            _faction.AddFaction(uid, SimpleHostileFaction);

            if (hasMind && mind != null && _player.TryGetSessionById(mind.UserId, out var session))
            {
                _chatMan.DispatchServerMessage(session, Loc.GetString("Вас поглотило безумие, вы больше не подвластны самому себе."));

                var position = Deleted(mind.OwnedEntity)
                    ? _gameTicker.GetObserverSpawnPoint().ToMap(EntityManager, _transform)
                    : _transform.GetMapCoordinates(mind.OwnedEntity.Value);

                var entity = Spawn(GameTicker.ObserverPrototypeName, position);
                EnsureComp<MindContainerComponent>(entity);
                var ghostComponent = Comp<GhostComponent>(entity);
                _ghosts.SetCanReturnToBody((entity, ghostComponent), false);

                _mindSystem.Visit(mindId, entity, mind);
                comp.Ghost = entity;
            }

            comp.IsCrazy = true;
        }

        private void UnCrazy(EntityUid uid, SanityComponent comp)
        {
            if (!comp.IsCrazy)
                return;

            _faction.ClearFactions(uid, dirty: false);

            if (!string.IsNullOrEmpty(comp.OldTask))
            {
                RemComp<HTNComponent>(uid);
                var htn = EnsureComp<HTNComponent>(uid);
                htn.RootTask = new HTNCompoundTask() { Task = comp.OldTask };
                htn.Blackboard.SetValue(NPCBlackboard.Owner, uid);
                _npc.WakeNPC(uid, htn);
                _faction.ClearFactions(uid, dirty: false);
                _faction.AddFaction(uid, SimpleHostileFaction);
                comp.OldTask = "";
            }
            else
            {
                RemComp<HTNComponent>(uid);
            }

            if (comp.OldFaction != null)
                _faction.AddFaction(uid, comp.OldFaction);

            if (!TryComp<GhostComponent>(comp.Ghost, out var ghostComponent))
                return;

            _ghosts.SetCanReturnToBody(ghostComponent, true);

            if (!_mindSystem.TryGetMind(comp.Ghost, out _, out var mind))
                return;

            if (_player.TryGetSessionById(mind.UserId, out var session))
                _chatMan.DispatchServerMessage(session, Loc.GetString("Вы можете вернуться в своё тело."));

            // RemComp<UnitologyEnslavedComponent>(uid); // если нужно раскомментировать

            comp.IsCrazy = false;
        }
    }
}
