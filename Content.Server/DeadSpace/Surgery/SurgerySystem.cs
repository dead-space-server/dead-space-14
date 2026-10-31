// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.OrganDamage;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Server.DeadSpace.Surgery;

public sealed class SurgerySystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    private static readonly TimeSpan IncisionDelay = TimeSpan.FromSeconds(3f);
    private static readonly TimeSpan OrganOperationDelay = TimeSpan.FromSeconds(5f);

    private static readonly DamageSpecifier ExtractionDamage = new()
    {
        DamageDict = { { "Bloodloss", 10 } },
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurgeryToolComponent, AfterInteractEvent>(OnToolInteract);
        SubscribeLocalEvent<SurgeryToolComponent, SurgeryDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<BodyIncisionComponent, ExaminedEvent>(OnExamine);
    }

    private void OnToolInteract(Entity<SurgeryToolComponent> tool, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { Valid: true } target)
            return;

        if (!TryComp<BodyComponent>(target, out var bodyComp))
            return;

        var zone = GetZone(target, args.ClickLocation);
        var action = GetAction(tool, args.User);

        if (!ValidateAction((target, bodyComp), zone, action, args.User, out var failureReason))
        {
            if (failureReason != null)
                _popup.PopupEntity(failureReason, target, args.User);
            return;
        }

        args.Handled = true;

        var doAfterEvent = new SurgeryDoAfterEvent { Action = action, Zone = zone };
        var delay = action == SurgeryAction.Incise ? IncisionDelay : OrganOperationDelay;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, delay, doAfterEvent, tool.Owner, target: target, used: tool.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });
    }

    private SurgeryAction GetAction(Entity<SurgeryToolComponent> tool, EntityUid user)
    {
        if (tool.Comp.Quality == SurgeryToolQuality.Incision)
            return SurgeryAction.Incise;

        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (held == tool.Owner)
                continue;

            if (HasComp<OrganComponent>(held))
                return SurgeryAction.Insert;
        }

        return SurgeryAction.Extract;
    }

    private bool ValidateAction(
        Entity<BodyComponent> target,
        BodyPartType zone,
        SurgeryAction action,
        EntityUid user,
        out string? failureReason)
    {
        failureReason = null;

        if (!TryGetZonePart(target, zone, out var partEnt, out var partComp) || partComp.Organs.Count == 0)
        {
            failureReason = Loc.GetString("surgery-zone-no-organs");
            return false;
        }

        switch (action)
        {
            case SurgeryAction.Incise:
            {
                if (TryComp<BodyIncisionComponent>(target, out var incision) && incision.IncisedParts.Contains(zone))
                {
                    failureReason = Loc.GetString("surgery-already-incised");
                    return false;
                }

                return true;
            }
            case SurgeryAction.Extract:
            {
                if (!IsIncised(target, zone))
                {
                    failureReason = Loc.GetString("surgery-needs-incision");
                    return false;
                }

                if (!FindOccupiedSlot(target, zone, out _, out _, out _, out _))
                {
                    failureReason = Loc.GetString("surgery-zone-empty");
                    return false;
                }

                return true;
            }
            case SurgeryAction.Insert:
            {
                if (!IsIncised(target, zone))
                {
                    failureReason = Loc.GetString("surgery-needs-incision");
                    return false;
                }

                if (!FindHeldOrgan(user, out var organ)
                    || !FindFreeSlot(target, zone, organ, out _, out _, out _))
                {
                    failureReason = Loc.GetString("surgery-no-fitting-slot");
                    return false;
                }

                return true;
            }
            default:
                return false;
        }
    }

    private bool IsIncised(EntityUid target, BodyPartType zone)
    {
        return TryComp<BodyIncisionComponent>(target, out var incision) && incision.IncisedParts.Contains(zone);
    }

    private void OnDoAfter(Entity<SurgeryToolComponent> tool, ref SurgeryDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { Valid: true } target)
            return;

        var zone = args.Zone;

        if (!TryComp<BodyComponent>(target, out var bodyComp))
            return;

        switch (args.Action)
        {
            case SurgeryAction.Incise:
                TryIncise(tool, (target, bodyComp), zone, args.User);
                break;
            case SurgeryAction.Extract:
                TryExtract(tool, (target, bodyComp), zone, args.User);
                break;
            case SurgeryAction.Insert:
                TryInsert(tool, (target, bodyComp), zone, args.User);
                break;
        }
    }

    private void TryIncise(Entity<SurgeryToolComponent> tool, Entity<BodyComponent> target, BodyPartType zone, EntityUid user)
    {
        if (!ValidateAction(target, zone, SurgeryAction.Incise, user, out var failure))
        {
            if (failure != null)
                _popup.PopupEntity(failure, target, user);
            return;
        }

        var incision = EnsureComp<BodyIncisionComponent>(target);
        incision.IncisedParts.Add(zone);
        Dirty(target, incision);

        _audio.PlayPvs(tool.Comp.IncisionSound, target);
        _popup.PopupEntity(Loc.GetString("surgery-incised", ("zone", LocZoneName(zone))), target, user);
    }

    private void TryExtract(Entity<SurgeryToolComponent> tool, Entity<BodyComponent> target, BodyPartType zone, EntityUid user)
    {
        if (!ValidateAction(target, zone, SurgeryAction.Extract, user, out var failure))
        {
            if (failure != null)
                _popup.PopupEntity(failure, target, user);
            return;
        }

        if (!FindOccupiedSlot(target, zone, out var partEnt, out var partComp, out var slotId, out var organUid))
            return;

        if (!_body.RemoveOrgan(organUid))
            return;

        _audio.PlayPvs(tool.Comp.ExtractSound, target);

        var organName = TryComp<MetaDataComponent>(organUid, out var meta) ? meta.EntityName : Loc.GetString("surgery-unknown-organ");
        _popup.PopupEntity(Loc.GetString("surgery-extracted", ("organ", organName)), target, user);

        if (!_hands.TryPickup(user, organUid))
        {
            _transform.SetCoordinates(organUid, Transform(target).Coordinates);
        }

        _damageable.TryChangeDamage(target.Owner, ExtractionDamage, ignoreResistances: true, interruptsDoAfters: false, origin: user);
    }

    private void TryInsert(Entity<SurgeryToolComponent> tool, Entity<BodyComponent> target, BodyPartType zone, EntityUid user)
    {
        if (!ValidateAction(target, zone, SurgeryAction.Insert, user, out var failure))
        {
            if (failure != null)
                _popup.PopupEntity(failure, target, user);
            return;
        }

        if (!FindHeldOrgan(user, out var organUid))
            return;

        if (!FindFreeSlot(target, zone, organUid, out var partEnt, out var partComp, out var slotId))
            return;

        if (!_body.InsertOrgan(partEnt, organUid, slotId, partComp))
            return;

        _audio.PlayPvs(tool.Comp.InsertSound, target);

        var organName = TryComp<MetaDataComponent>(organUid, out var meta) ? meta.EntityName : Loc.GetString("surgery-unknown-organ");
        _popup.PopupEntity(Loc.GetString("surgery-inserted", ("organ", organName)), target, user);
    }

    private BodyPartType GetZone(EntityUid target, EntityCoordinates clickLocation)
    {
        var xform = Transform(target);
        var mapClick = _transform.ToMapCoordinates(clickLocation);
        var local = (-xform.WorldRotation).RotateVec(mapClick.Position - xform.WorldPosition);

        return local.Y >= 0.75f ? BodyPartType.Head : BodyPartType.Torso;
    }

    private bool TryGetZonePart(
        Entity<BodyComponent> body,
        BodyPartType zone,
        out EntityUid partEnt,
        out BodyPartComponent partComp)
    {
        partEnt = default;
        partComp = default!;

        foreach (var part in _body.GetBodyChildrenOfType(body.Owner, zone, body.Comp))
        {
            if (part.Component.Organs.Count == 0)
                continue;

            partEnt = part.Id;
            partComp = part.Component;
            return true;
        }

        return false;
    }

    private bool FindOccupiedSlot(
        Entity<BodyComponent> body,
        BodyPartType zone,
        out EntityUid partEnt,
        out BodyPartComponent partComp,
        out string slotId,
        out EntityUid organUid)
    {
        partEnt = default;
        partComp = default!;
        slotId = string.Empty;
        organUid = default;

        foreach (var part in _body.GetBodyChildrenOfType(body.Owner, zone, body.Comp))
        {
            foreach (var (id, slot) in part.Component.Organs)
            {
                var containerId = SharedBodySystem.GetOrganContainerId(id);
                if (!_containers.TryGetContainer(part.Id, containerId, out var container) ||
                    container is not ContainerSlot slotContainer ||
                    slotContainer.ContainedEntity is not { Valid: true } organ)
                {
                    continue;
                }

                partEnt = part.Id;
                partComp = part.Component;
                slotId = id;
                organUid = organ;
                return true;
            }
        }

        return false;
    }

    private bool FindHeldOrgan(EntityUid user, out EntityUid organUid)
    {
        organUid = default;

        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (!HasComp<OrganComponent>(held))
                continue;

            organUid = held;
            return true;
        }

        return false;
    }

    private bool FindFreeSlot(
        Entity<BodyComponent> body,
        BodyPartType zone,
        EntityUid organ,
        out EntityUid partEnt,
        out BodyPartComponent partComp,
        out string slotId)
    {
        partEnt = default;
        partComp = default!;
        slotId = string.Empty;

        var organProto = TryComp<MetaDataComponent>(organ, out var meta) ? meta.EntityPrototype?.ID : null;

        foreach (var part in _body.GetBodyChildrenOfType(body.Owner, zone, body.Comp))
        {
            foreach (var (id, _) in part.Component.Organs)
            {
                var containerId = SharedBodySystem.GetOrganContainerId(id);
                if (_containers.TryGetContainer(part.Id, containerId, out var container) &&
                    container is ContainerSlot slotContainer &&
                    slotContainer.ContainedEntity != null)
                {
                    continue;
                }

                if (!MatchesSlot(organ, organProto, id))
                    continue;

                partEnt = part.Id;
                partComp = part.Component;
                slotId = id;
                return true;
            }
        }

        return false;
    }

    private bool MatchesSlot(EntityUid organ, string? organProto, string slotId)
    {
        if (TryComp<OrganDamageableComponent>(organ, out var organDamage) && organDamage.SurgerySlotId is { } explicitSlot)
            return explicitSlot == slotId;

        if (organProto == null)
            return false;

        return organProto.ToLowerInvariant().Contains(slotId.ToLowerInvariant());
    }

    private string LocZoneName(BodyPartType zone)
    {
        return zone switch
        {
            BodyPartType.Head => Loc.GetString("surgery-zone-head"),
            BodyPartType.Torso => Loc.GetString("surgery-zone-torso"),
            _ => zone.ToString(),
        };
    }

    private void OnExamine(Entity<BodyIncisionComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.IncisedParts.Count == 0)
            return;

        var parts = new List<string>();
        foreach (var zone in ent.Comp.IncisedParts)
        {
            parts.Add(LocZoneName(zone));
        }

        args.PushMarkup(Loc.GetString("surgery-examine-incised", ("parts", string.Join(", ", parts))));
    }
}
