// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Diagnostics.CodeAnalysis;
using Content.Server.Cloning;
using Content.Shared.Clothing;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Content.Shared.Ninja.Components;
using Content.Server.Ninja.Systems;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class NinjaSecondChanceSystem : EntitySystem
{
    private const string CloneSettingsId = "NinjaSecondChanceClone";
    private const string BodyContainerId = "ninja_capsule_body";
    private const string GearId = "SpaceNinjaGear";
    private const string SurvivalLoadoutId = "RoleSurvivalSpaceNinja";

    [Dependency] private readonly CloningSystem _cloning = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SpaceNinjaSystem _ninja = default!;
    [Dependency] private readonly SpiderOSSystem _spiderOS = default!;
    [Dependency] private readonly LoadoutSystem _loadout = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NinjaSecondChanceComponent, MapInitEvent>(OnSecondChanceInit);
        SubscribeLocalEvent<NinjaSecondChanceComponent, AutoDustEvent>(OnAutoDust);
        SubscribeLocalEvent<NinjaRespawnCapsuleComponent, ComponentInit>(OnCapsuleInit);
    }

    private void OnSecondChanceInit(Entity<NinjaSecondChanceComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Capsule != null || !TryFindCapsule(ent, out var capsuleUid))
            return;

        ent.Comp.Capsule = capsuleUid;
        Dirty(ent);
    }

    private void OnCapsuleInit(Entity<NinjaRespawnCapsuleComponent> ent, ref ComponentInit args)
    {
        ent.Comp.BodyContainer = _container.EnsureContainer<ContainerSlot>(ent.Owner, BodyContainerId);
        _appearance.SetData(ent.Owner, NinjaRespawnCapsuleVisuals.Full, false);
    }

    private void OnAutoDust(Entity<NinjaSecondChanceComponent> ent, ref AutoDustEvent args)
    {
        if (ent.Comp.Used)
            return;

        var target = args.Target;
        if (!target.IsValid())
            return;

        if (!TryGetDustMind(args, target, out var mindId, out var mind))
            return;

        if (!TryComp<NinjaRespawnCapsuleComponent>(ent.Comp.Capsule, out var capsule) || !IsCapsuleFree(capsule))
        {
            if (!TryFindCapsule(target, out var found))
                return;

            ent.Comp.Capsule = found;
            capsule = Comp<NinjaRespawnCapsuleComponent>(found.Value);
        }

        var capsuleUid = ent.Comp.Capsule!.Value;

        if (!_cloning.TryCloning(target, null, CloneSettingsId, out var clone))
            return;

        ent.Comp.Used = true;
        Dirty(ent);

        RestoreSuitOnClone(clone.Value, args.SpiderOS);

        if (capsule.BodyContainer.ContainedEntity is { Valid: true } occupant && Exists(occupant))
        {
            _container.Remove(occupant, capsule.BodyContainer, force: true);
            _audio.PlayPvs(capsule.ExitSound, capsuleUid);
        }

        _container.Insert(clone.Value, capsule.BodyContainer, force: true);
        capsule.Timer = 0f;
        _appearance.SetData(capsuleUid, NinjaRespawnCapsuleVisuals.Full, true);
        _audio.PlayPvs(capsule.EnterSound, capsuleUid);

        _mind.TransferTo(mindId, clone, ghostCheckOverride: true, mind: mind);
    }

    private bool TryGetDustMind(
        AutoDustEvent args,
        EntityUid target,
        out EntityUid mindId,
        [NotNullWhen(true)] out MindComponent? mind)
    {
        if (args.Mind is { } eventMind && eventMind.Owner.IsValid() && eventMind.Comp != null && Exists(eventMind.Owner))
        {
            mindId = eventMind.Owner;
            mind = eventMind.Comp;
            return true;
        }

        return _mind.TryGetMind(target, out mindId, out mind);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NinjaRespawnCapsuleComponent>();
        while (query.MoveNext(out var uid, out var capsule))
        {
            if (capsule.BodyContainer.ContainedEntity is not { Valid: true } contained || !Exists(contained))
            {
                if (capsule.Timer > 0f)
                {
                    capsule.Timer = 0f;
                    _appearance.SetData(uid, NinjaRespawnCapsuleVisuals.Full, false);
                }

                continue;
            }

            capsule.Timer += frameTime;
            if (capsule.Timer < capsule.RespawnTime)
                continue;

            _container.Remove(contained, capsule.BodyContainer, force: true);
            capsule.Timer = 0f;
            _appearance.SetData(uid, NinjaRespawnCapsuleVisuals.Full, false);
            _audio.PlayPvs(capsule.ExitSound, uid);
        }
    }

    private void RestoreSuitOnClone(EntityUid clone, SpiderOSComponent? source)
    {
        _loadout.Equip(clone, new() { GearId }, new() { SurvivalLoadoutId });

        var ninja = EnsureComp<SpaceNinjaComponent>(clone);

        if (FindClonedSuit(clone) is not { } suitUid)
            return;

        _ninja.AssignSuit(new Entity<SpaceNinjaComponent>(clone, ninja), suitUid);

        if (source == null)
            return;

        _spiderOS.RestoreState(suitUid, source);
    }

    private EntityUid? FindClonedSuit(EntityUid clone)
    {
        if (!TryComp<InventoryComponent>(clone, out var inventory))
            return null;

        var enumerator = _inventory.GetSlotEnumerator((clone, inventory));
        while (enumerator.NextItem(out var item, out _))
        {
            if (TryComp<SpiderOSComponent>(item, out _))
                return item;
        }

        return null;
    }

    private bool TryFindCapsule(EntityUid target, [NotNullWhen(true)] out EntityUid? capsuleUid)
    {
        capsuleUid = null;

        var targetCoord = _transform.GetMapCoordinates(target);

        if (targetCoord.MapId == MapId.Nullspace)
            return TryFindAnyCapsule(out capsuleUid);

        var targetPos = targetCoord.Position;
        EntityUid? nearestFree = null;
        EntityUid? nearestTaken = null;
        var freeDistSqr = float.MaxValue;
        var takenDistSqr = float.MaxValue;

        var query = EntityQueryEnumerator<NinjaRespawnCapsuleComponent>();
        while (query.MoveNext(out var uid, out var capsule))
        {
            var coord = _transform.GetMapCoordinates(uid);
            if (coord.MapId != targetCoord.MapId)
                continue;

            var distSqr = (coord.Position - targetPos).LengthSquared();

            if (IsCapsuleFree(capsule))
            {
                if (distSqr < freeDistSqr)
                {
                    freeDistSqr = distSqr;
                    nearestFree = uid;
                }

                continue;
            }

            if (distSqr < takenDistSqr)
            {
                takenDistSqr = distSqr;
                nearestTaken = uid;
            }
        }

        capsuleUid = nearestFree ?? nearestTaken;
        return capsuleUid != null;
    }

    private bool TryFindAnyCapsule([NotNullWhen(true)] out EntityUid? capsuleUid)
    {
        capsuleUid = null;

        var query = EntityQueryEnumerator<NinjaRespawnCapsuleComponent>();
        if (!query.MoveNext(out var uid, out _))
            return false;

        capsuleUid = uid;
        return true;
    }

    private bool IsCapsuleFree(NinjaRespawnCapsuleComponent capsule)
    {
        return capsule.BodyContainer.ContainedEntity is not { Valid: true } contained || !Exists(contained);
    }
}