// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Administration.Logs;
using Content.Shared.Changeling.Systems;
using Content.Shared.Database;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Prototypes;
using Content.Shared.Body.Systems;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Content.Shared.Humanoid;
using Content.Shared.Rotation;
using Content.Shared.Standing;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Server.DeadSpace.Surgery;

public sealed class SurgeryLimbPort : EntitySystem
{
    [Dependency] private readonly LimbWoundSystem _limbs = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SurgeryOrganSystem _organs = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly SurgeryOrganFreshSystem _fresh = default!;
    [Dependency] private readonly SurgeryGraftSystem _graft = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly BloodstreamSystem _blood = default!;
    [Dependency] private readonly SharedProjectileSystem _projectiles = default!;

    private const float Detox = 0.6f;

    private readonly HashSet<EntityUid> _restock = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyComponent, Content.Shared.Rejuvenate.RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<RegenerativeStasisExitedEvent>(OnStasis);
    }

    private void OnStasis(ref RegenerativeStasisExitedEvent args)
    {
        Restore(args.Target);
    }

    public void Restore(EntityUid uid)
    {
        if (!TryComp<LimbBodyComponent>(uid, out _))
            return;

        foreach (var zone in new[]
                 {
                     LimbZone.Head,
                     LimbZone.ArmLeft, LimbZone.ArmRight, LimbZone.LegLeft, LimbZone.LegRight,
                     LimbZone.HandLeft, LimbZone.HandRight, LimbZone.FootLeft, LimbZone.FootRight,
                 })
            Grow(uid, zone);

        _limbs.ClearInsides(uid);
        RemComp<SurgeryInfectionComponent>(uid);
        RemComp<SurgeryRejectionComponent>(uid);
        Seal(uid);
        PullShards(uid);
        if (TryComp<BloodstreamComponent>(uid, out var blood))
        {
            _blood.TryModifyBloodLevel(uid, blood.BloodReferenceSolution.MaxVolume);
            _blood.TryModifyBleedAmount(uid, -blood.BleedAmount);
        }

        _restock.Add(uid);
    }

    private void Seal(EntityUid uid)
    {
        if (!TryComp<SurgerySiteComponent>(uid, out var site))
            return;

        site.SpringBone = 0f;
        site.Done = true;
        site.Part = "";
        site.Queued = "";
        site.Step = 0;
        site.CloseStep = 0;
        site.Clamped = false;
        site.Chosen = 255;
        site.Cut = 0f;
        for (var i = 0; i < site.Layers.Count; i++)
        {
            var layer = site.Layers[i];
            layer.Integrity = 1f;
            layer.Retracted = false;
            site.Layers[i] = layer;
        }

        Dirty(uid, site);
    }

    private void PullShards(EntityUid uid)
    {
        if (!TryComp<EmbeddedContainerComponent>(uid, out var embedded))
            return;

        var stuck = new EntityUid[embedded.EmbeddedObjects.Count];
        embedded.EmbeddedObjects.CopyTo(stuck);
        foreach (var shard in stuck)
            _projectiles.EmbedDetach(shard, null);
    }

    private void Grow(EntityUid uid, LimbZone zone)
    {
        if (TryComp<LimbBodyComponent>(uid, out var limbs) && !_limbs.HasPart(uid, zone))
        {
            var index = LimbWoundRules.Index(zone);
            if ((uint) index < (uint) limbs.Zones.Count)
            {
                var state = limbs.Zones[index];
                state.Severed = true;
                limbs.Zones[index] = state;
            }
        }

        _limbs.TryRegrowZone(uid, zone, 1f);
    }

    private void OnRejuvenate(EntityUid uid, BodyComponent body, Content.Shared.Rejuvenate.RejuvenateEvent args)
    {
        _restock.Add(uid);
        RemComp<SurgeryInfectionComponent>(uid);
        RemComp<SurgeryRejectionComponent>(uid);
    }

    public override void Update(float frameTime)
    {
        if (_restock.Count == 0)
            return;

        foreach (var body in _restock)
            RestockOrgans(body);
        _restock.Clear();
    }

    private void RestockOrgans(EntityUid body)
    {
        if (TerminatingOrDeleted(body)
            || !TryComp<BodyComponent>(body, out var comp)
            || comp.Prototype is not { } protoId
            || !_prototype.TryIndex(protoId, out BodyPrototype? prototype))
            return;

        var any = false;
        foreach (var slot in prototype.Slots.Values)
        {
            foreach (var (organSlot, organId) in slot.Organs)
            {
                if (!TryPart(body, organSlot, out var part)
                    || !_containers.TryGetContainer(part, SharedBodySystem.GetOrganContainerId(organSlot), out var container)
                    || container.ContainedEntities.Count > 0)
                    continue;

                var organ = Spawn(organId, Transform(body).Coordinates);
                if (_body.InsertOrgan(part, organ, organSlot))
                    any = true;
                else
                    QueueDel(organ);
            }
        }

        if (any)
            _organs.Refresh(body);
    }

    private Vector2? CutAt(EntityUid patient, LimbZone zone)
    {
        if (!_prototype.TryIndex<SurgeryMarkPrototype>(zone.ToString(), out var mark))
            return null;

        var x = (mark.Ax + mark.Bx) / 2f;
        var y = (mark.Ay + mark.By) / 2f;
        if (HasComp<SurgeryProneComponent>(patient))
            x = 31f - x;

        var local = new Vector2((x - 16f) / 32f, (16f - y) / 32f);
        var turn = _standing.IsDown(patient) && TryComp<RotationVisualsComponent>(patient, out var rotation)
            ? rotation.HorizontalRotation
            : Angle.Zero;
        return turn.RotateVec(local);
    }

    public void Apply(EntityUid patient, LimbZone zone, LimbOrgan? organ, in SurgeryResult result, EntityUid? held, EntityUid? user = null)
    {
        if (result.Rejected)
            return;

        if (LimbWoundRules.IsBody(zone))
        {
            _limbs.ApplyEdit(patient, zone, new LimbEdit
            {
                Flesh = result.Flesh,
                Bone = result.Bone,
                Skin = result.Skin,
                Bleed = result.Bleed,
                Scar = result.Scar,
                Health = result.Health,
                SetHealth = result.SetHealth,
                ClearAtrophy = result.ClearAtrophy,
                ClearEmbedded = result.ClearEmbedded,
                ClearSkinBare = result.ClearSkinBare,
            });
        }

        if (organ is { } picked)
        {
            if (result.RemoveOrgan)
                Take(patient, picked, result, user);

            if (result.PlaceOrgan && picked == LimbOrgan.Eyes
                && TryComp<LimbBodyComponent>(patient, out var seeing) && seeing.EyesGone > 0
                && (uint) LimbOrgan.Eyes < (uint) seeing.Organs.Count && !seeing.Organs[(int) LimbOrgan.Eyes].Missing
                && held is { } spare)
            {
                if (_graft.Accepts(patient, spare))
                {
                    _graft.Grafted(patient, spare, LimbOrganHomes.Home(picked), picked.ToString(), null);
                    _limbs.ApplyOrganEdit(patient, picked, new LimbOrganEdit { EyesGone = -seeing.EyesGone });
                    if (user is { } inserter)
                        _adminLog.Add(LogType.Surgery, LogImpact.Medium, $"{ToPrettyString(inserter):user} inserted {ToPrettyString(spare):entity} as {picked} into {ToPrettyString(patient):target}");
                    Del(spare);
                }
            }
            else if (result.PlaceOrgan && held is { } item)
                Place(patient, picked, item, result, user);

            if (!result.RemoveOrgan && !result.PlaceOrgan && (result.OrganDamage > 0f || result.Rupture || result.Eyes != 0))
            {
                _limbs.ApplyOrganEdit(patient, picked, new LimbOrganEdit
                {
                    Damage = result.OrganDamage,
                    SetRuptured = result.Rupture,
                    Ruptured = true,
                    EyesGone = result.Eyes,
                });
            }
        }

        if (organ != null && (result.RemoveOrgan || result.PlaceOrgan))
            _organs.Refresh(patient);

        if (result.Sever && _limbs.Amputate(patient, zone, CutAt(patient, zone)) && user is { } cutter)
            _adminLog.Add(LogType.Surgery, LogImpact.High, $"{ToPrettyString(cutter):user} amputated {zone} of {ToPrettyString(patient):target}");

        if (result.Attach && held is { } part)
        {
            var name = ToPrettyString(part);
            if (_limbs.TryReattach(patient, zone, part, result.Crude) && user is { } sewer)
                _adminLog.Add(LogType.Surgery, LogImpact.Medium, $"{ToPrettyString(sewer):user} attached {name:entity} to {zone} of {ToPrettyString(patient):target}");
        }
    }

    private void Take(EntityUid patient, LimbOrgan organ, in SurgeryResult result, EntityUid? user)
    {
        var edit = new LimbOrganEdit
        {
            Damage = result.OrganDamage,
            SetRuptured = result.Rupture,
            Ruptured = result.Rupture,
            SetMissing = true,
            Missing = true,
            EyesGone = organ == LimbOrgan.Eyes ? 2 : result.Eyes,
        };

        if (!TryOrgan(patient, organ, out var entity))
            return;

        EnsureComp<SurgeryKeepOrganComponent>(entity);
        var removed = _body.RemoveOrgan(entity);
        RemComp<SurgeryKeepOrganComponent>(entity);
        if (!removed)
            return;

        Stamp(entity, patient, organ, result);
        if (user is { } taker)
            _adminLog.Add(LogType.Surgery, LogImpact.High, $"{ToPrettyString(taker):user} removed {ToPrettyString(entity):entity} ({organ}) from {ToPrettyString(patient):target}");
        _transform.SetCoordinates(entity, Transform(patient).Coordinates.Offset(new Vector2(0.45f, 0.15f)));
        _limbs.ApplyOrganEdit(patient, organ, edit);
        if (_limbs.IsVital(patient, organ))
        {
            var zone = LimbOrganHomes.Home(organ);
            _limbs.ApplyEdit(patient, zone, new LimbEdit { SetHealth = true, Health = 0f });
            _limbs.KillByLimbs(patient);
        }
    }

    private void Place(EntityUid patient, LimbOrgan organ, EntityUid item, in SurgeryResult result, EntityUid? user)
    {
        if (!TryComp<OrganComponent>(item, out _))
            return;

        if (!SurgeryOrganKindComponent.Matches(EntityManager, item, organ.ToString(), Machine(patient)))
            return;

        if (!_prototype.TryIndex<SurgeryOrganPrototype>(organ.ToString(), out var slot))
            return;

        if (!TryPart(patient, slot.Slot, out var part))
            return;

        if (!_graft.Accepts(patient, item))
            return;

        var decay = _fresh.Spoil(item);
        if (!_body.InsertOrgan(part, item, slot.Slot))
            return;

        _graft.Grafted(patient, item, LimbOrganHomes.Home(organ), organ.ToString(), null);
        if (user is { } placer)
            _adminLog.Add(LogType.Surgery, LogImpact.Medium, $"{ToPrettyString(placer):user} inserted {ToPrettyString(item):entity} as {organ} into {ToPrettyString(patient):target}");
        var rotten = decay >= 1f;
        _limbs.ApplyOrganEdit(patient, organ, new LimbOrganEdit
        {
            SetMissing = true,
            Missing = false,
            SetDamage = true,
            Damage = MathF.Max(result.Rupture ? LimbOrganRules.Rupture : 0f, decay * LimbOrganRules.Rupture),
            SetRuptured = true,
            Ruptured = result.Rupture || rotten,
            EyesGone = organ == LimbOrgan.Eyes ? -2 : 0,
        });
        if (rotten && HasComp<BrainComponent>(item))
            _fresh.MarkRotten(patient);

        if (!rotten && !result.Rupture && organ is LimbOrgan.Kidneys or LimbOrgan.Liver)
            Cleanse(patient, 1f - decay);
    }

    private void Cleanse(EntityUid patient, float share)
    {
        if (share <= 0f
            || !TryComp<DamageableComponent>(patient, out var damageable)
            || !damageable.Damage.DamageDict.TryGetValue("Poison", out var poison)
            || poison <= 0)
            return;

        var heal = new DamageSpecifier();
        heal.DamageDict["Poison"] = -poison * Detox * share;
        _damage.TryChangeDamage(patient, heal, true, false);
    }

    public bool Regrow(EntityUid patient, LimbOrgan organ)
    {
        if (!TryComp<BodyComponent>(patient, out var body)
            || body.Prototype is not { } protoId
            || !_prototype.TryIndex(protoId, out BodyPrototype? prototype)
            || !_prototype.TryIndex<SurgeryOrganPrototype>(organ.ToString(), out var slot))
            return false;

        string? grown = null;
        foreach (var bodySlot in prototype.Slots.Values)
        {
            if (bodySlot.Organs.TryGetValue(slot.Slot, out var id))
            {
                grown = id;
                break;
            }
        }

        if (grown == null || !TryPart(patient, slot.Slot, out _))
            return false;

        var item = Spawn(grown, Transform(patient).Coordinates);
        Place(patient, organ, item, new SurgeryResult(), null);
        if (!TerminatingOrDeleted(item) && !_containers.IsEntityInContainer(item))
        {
            QueueDel(item);
            return false;
        }

        return true;
    }

    private void Stamp(EntityUid organ, EntityUid patient, LimbOrgan kind, in SurgeryResult result)
    {
        var finding = EnsureComp<OrganFindingComponent>(organ);
        finding.Donor = Name(patient);
        finding.Organ = kind.ToString();
        finding.Ruptured = result.Rupture;
        if (TryComp<LimbBodyComponent>(patient, out var limbs))
        {
            var index = (int) kind;
            if ((uint) index < (uint) limbs.Organs.Count)
                finding.Damage = limbs.Organs[index].Damage + result.OrganDamage;
        }
    }

    private bool TryOrgan(EntityUid patient, LimbOrgan organ, out EntityUid entity)
    {
        entity = default;
        if (!TryComp<BodyComponent>(patient, out var body))
            return false;

        var machine = Machine(patient);
        foreach (var organEnt in _body.GetBodyOrgans(patient, body))
        {
            if (!SurgeryOrganKindComponent.Matches(EntityManager, organEnt.Id, organ.ToString(), machine))
                continue;

            entity = organEnt.Id;
            return true;
        }

        return false;
    }

    private bool Machine(EntityUid patient)
    {
        return TryComp<HumanoidAppearanceComponent>(patient, out var look) && SurgerySpeciesPrototype.IsMachine(_prototype, look.Species.Id);
    }

    private bool TryPart(EntityUid patient, string slot, out EntityUid part)
    {
        part = default;
        foreach (var child in _body.GetBodyChildren(patient))
        {
            var containerId = SharedBodySystem.GetOrganContainerId(slot);
            if (!_containers.TryGetContainer(child.Id, containerId, out _))
                continue;

            part = child.Id;
            return true;
        }

        return false;
    }
}
