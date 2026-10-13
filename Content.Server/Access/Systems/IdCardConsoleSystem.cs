using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Containers;
using Content.Server.DeadSpace.CentComm;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared.Access.Components;
using static Content.Shared.Access.Components.IdCardConsoleComponent;
using Content.Shared.Access.Systems;
using Content.Shared.Access;
using Content.Shared.Administration.Logs;
using Content.Shared.Chat;
using Content.Shared.Construction;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DeadSpace.Access;
using Content.Shared.Roles;
using Content.Shared.StationRecords;
using Content.Shared.Throwing;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Access.Systems;

[UsedImplicitly]
public sealed class IdCardConsoleSystem : SharedIdCardConsoleSystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly StationRecordsSystem _record = default!;
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly AccessSystem _access = default!;
    [Dependency] private readonly IdCardSystem _idCard = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly StationSystem _station = default!; // DS14

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IdCardConsoleComponent, WriteToTargetIdMessage>(OnWriteToTargetIdMessage);

        // one day, maybe bound user interfaces can be shared too.
        SubscribeLocalEvent<IdCardConsoleComponent, ComponentStartup>(UpdateUserInterface);
        SubscribeLocalEvent<IdCardConsoleComponent, EntInsertedIntoContainerMessage>(UpdateUserInterface);
        SubscribeLocalEvent<IdCardConsoleComponent, EntRemovedFromContainerMessage>(UpdateUserInterface);
        SubscribeLocalEvent<IdCardConsoleComponent, BoundUIOpenedEvent>(UpdateUserInterface); // DS14
        SubscribeLocalEvent<IdCardConsoleComponent, DamageChangedEvent>(OnDamageChanged);

        // Intercept the event before anyone can do anything with it!
        SubscribeLocalEvent<IdCardConsoleComponent, MachineDeconstructedEvent>(OnMachineDeconstructed,
            before: [typeof(EmptyOnMachineDeconstructSystem), typeof(ItemSlotsSystem)]);
    }

    private void OnWriteToTargetIdMessage(EntityUid uid, IdCardConsoleComponent component, WriteToTargetIdMessage args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        TryWriteToTargetId(uid, args.FullName, args.JobTitle, args.AccessList, args.JobPrototype, player, component);

        UpdateUserInterface(uid, component, args);
    }

    private void UpdateUserInterface(EntityUid uid, IdCardConsoleComponent component, EntityEventArgs args)
    {
        if (!component.Initialized)
            return;

        var privilegedIdName = string.Empty;
        // DS14-start
        string? privilegedFullName = null;
        string? privilegedJobTitle = null;
        // DS14-end
        List<ProtoId<AccessLevelPrototype>>? possibleAccess = null;
        if (component.PrivilegedIdSlot.Item is { Valid: true } item)
        {
            privilegedIdName = Comp<MetaDataComponent>(item).EntityName;
            // DS14-start
            if (TryComp<IdCardComponent>(item, out var authorizationCard))
            {
                privilegedFullName = authorizationCard.FullName;
                privilegedJobTitle = authorizationCard.LocalizedJobTitle;
            }
            // DS14-end
            possibleAccess = _accessReader.FindAccessTags(item).ToList();
        }

        IdCardConsoleBoundUserInterfaceState newState;
        // this could be prettier
        if (component.TargetIdSlot.Item is not { Valid: true } targetId)
        {
            newState = new IdCardConsoleBoundUserInterfaceState(
                component.PrivilegedIdSlot.HasItem,
                PrivilegedIdIsAuthorized(uid, component, out _),
                false,
                null,
                null,
                null,
                possibleAccess,
                string.Empty,
                privilegedIdName,
                string.Empty,
                GetAvailableAccess(uid, component).ToList(), privilegedFullName, privilegedJobTitle); // DS14
        }
        else
        {
            var targetIdComponent = Comp<IdCardComponent>(targetId);
            var targetAccessComponent = Comp<AccessComponent>(targetId);

            var jobProto = targetIdComponent.JobPrototype ?? new ProtoId<JobPrototype>(string.Empty);
            if (TryComp<StationRecordKeyStorageComponent>(targetId, out var keyStorage)
                && keyStorage.Key is { } key
                && _record.TryGetRecord<GeneralStationRecord>(key, out var record))
            {
                jobProto = record.JobPrototype;
            }

            newState = new IdCardConsoleBoundUserInterfaceState(
                component.PrivilegedIdSlot.HasItem,
                PrivilegedIdIsAuthorized(uid, component, out _),
                true,
                targetIdComponent.FullName,
                targetIdComponent.LocalizedJobTitle,
                targetAccessComponent.Tags.ToList(),
                possibleAccess,
                jobProto,
                privilegedIdName,
                Name(targetId),
                GetAvailableAccess(uid, component).ToList(), privilegedFullName, privilegedJobTitle); // DS14
        }

        _userInterface.SetUiState(uid, IdCardConsoleUiKey.Key, newState);
    }

    // DS14-start
    private HashSet<ProtoId<AccessLevelPrototype>> GetAvailableAccess(EntityUid uid, IdCardConsoleComponent component)
    {
        var access = component.AccessLevels.ToHashSet();
        var onCentComm = HasComp<CentCommStationComponent>(_station.GetOwningStation(uid));
        foreach (var category in _prototype.EnumeratePrototypes<IdCardAccessCategoryPrototype>())
        {
            if (!category.CentCommOnly)
                continue;

            if (onCentComm)
                access.UnionWith(category.AccessLevels);
            else
                access.ExceptWith(category.AccessLevels);
        }

        access.RemoveWhere(id => !_prototype.Resolve(id, out var level) || !level.CanAddToIdCard);
        return access;
    }
    // DS14-end

    /// <summary>
    /// Called whenever an access button is pressed, adding or removing that access from the target ID card.
    /// Writes data passed from the UI into the ID stored in <see cref="IdCardConsoleComponent.TargetIdSlot"/>, if present.
    /// </summary>
    private void TryWriteToTargetId(EntityUid uid,
        string newFullName,
        string newJobTitle,
        List<ProtoId<AccessLevelPrototype>> newAccessList,
        ProtoId<JobPrototype> newJobProto,
        EntityUid player,
        IdCardConsoleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.TargetIdSlot.Item is not { Valid: true } targetId || !PrivilegedIdIsAuthorized(uid, component, out var privilegedId))
            return;

        // DS14-start
        // Validate changes before mutating the identity or consuming a vacancy. Unchanged hidden access is preserved.
        var oldTags = _access.TryGetTags(targetId)?.ToHashSet() ?? new HashSet<ProtoId<AccessLevelPrototype>>();
        var requestedTags = newAccessList.ToHashSet();
        var difference = oldTags.ToHashSet();
        difference.SymmetricExceptWith(requestedTags);
        var availableAccess = GetAvailableAccess(uid, component);
        var privilegedPerms = _accessReader.FindAccessTags(privilegedId.Value);
        if (!difference.IsSubsetOf(availableAccess) || !difference.IsSubsetOf(privilegedPerms))
        {
            _adminLogger.Add(LogType.Action, LogImpact.High,
                $"{ToPrettyString(player):player} tried to change unavailable access on {ToPrettyString(targetId):entity} via {ToPrettyString(uid):entity}");
            return;
        }
        // DS14-end

        // DS14-start: validate and authorize a requested job before mutating any part of the ID.
        JobPrototype? job = null;
        var jobChanged = false;
        if (!string.IsNullOrEmpty(newJobProto.Id))
        {
            // The client only lists console-visible jobs, but the server must enforce the same rule
            // so a modified client cannot assign hidden prototypes such as Dismissed.
            if (!_prototype.Resolve(newJobProto, out job)
                || !job.OverrideConsoleVisibility.GetValueOrDefault(job.SetPreference)
                || !_prototype.Resolve(job.Icon, out var jobIcon))
            {
                return;
            }

            jobChanged = GetCurrentJob(targetId) != newJobProto;
            if (jobChanged)
            {
                var jobAttempt = new IdCardJobAssignmentAttemptEvent(player, targetId, newJobProto);
                RaiseLocalEvent(jobAttempt);

                if (jobAttempt.Cancelled)
                    return;
            }

            _idCard.TryChangeJobIcon(targetId, jobIcon, player: player);
            _idCard.TryChangeJobDepartment(targetId, job);
        }
        // DS14-end

        _idCard.TryChangeFullName(targetId, newFullName, player: player);
        _idCard.TryChangeJobTitle(targetId, newJobTitle, player: player);

        UpdateStationRecord(uid, targetId, newFullName, newJobTitle, job);
        if ((!TryComp<StationRecordKeyStorageComponent>(targetId, out var keyStorage)
            || keyStorage.Key is not { } key
            || !_record.TryGetRecord<GeneralStationRecord>(key, out _))
            && job != null)
        {
            Comp<IdCardComponent>(targetId).JobPrototype = job.ID;
        }

        // DS14-start: only a confirmed, real transition may consume a vacancy slot. A generic
        // RecordModifiedEvent cannot distinguish a job assignment from a name/status edit.
        if (jobChanged)
            RaiseLocalEvent(new IdCardJobAssignedEvent(player, targetId, newJobProto));
        // DS14-end

        // DS14-start
        // The old full-list check rejected existing access that this console cannot edit:
        // if (!newAccessList.TrueForAll(x => component.AccessLevels.Contains(x)))
        // Authorization now checks the difference before any card data is written.
        if (difference.Count == 0)
            return;
        // DS14-end

        var addedTags = newAccessList.Except(oldTags).Select(tag => "+" + tag).ToList();
        var removedTags = oldTags.Except(newAccessList).Select(tag => "-" + tag).ToList();
        _access.TrySetTags(targetId, newAccessList);

        /*TODO: ECS SharedIdCardConsoleComponent and then log on card ejection, together with the save.
        This current implementation is pretty shit as it logs 27 entries (27 lines) if someone decides to give themselves AA*/
        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(player):player} modified accesses on {ToPrettyString(targetId):entity} via {ToPrettyString(uid):entity}: [{string.Join(", ", addedTags.Union(removedTags))}] resulting accesses: [{string.Join(", ", newAccessList)}]");
    }

    /// <summary>
    /// Returns true if there is an ID in <see cref="IdCardConsoleComponent.PrivilegedIdSlot"/> and said ID satisfies the requirements of <see cref="AccessReaderComponent"/>.
    /// </summary>
    private bool PrivilegedIdIsAuthorized(EntityUid uid, IdCardConsoleComponent component, [NotNullWhen(true)] out EntityUid? id)
    {
        id = null;
        if (component.PrivilegedIdSlot.Item == null)
            return false;

        id = component.PrivilegedIdSlot.Item;
        if (!TryComp<AccessReaderComponent>(uid, out var reader))
            return true;

        return _accessReader.IsAllowed(id.Value, uid, reader);
    }

    // DS14-start
    private ProtoId<JobPrototype>? GetCurrentJob(EntityUid targetId)
    {
        if (TryComp<StationRecordKeyStorageComponent>(targetId, out var keyStorage)
            && keyStorage.Key is { } key
            && _record.TryGetRecord<GeneralStationRecord>(key, out var record))
        {
            return record.JobPrototype;
        }

        return TryComp<IdCardComponent>(targetId, out var idCard)
            ? idCard.JobPrototype
            : null;
    }
    // DS14-end

    private void UpdateStationRecord(EntityUid uid, EntityUid targetId, string newFullName, ProtoId<AccessLevelPrototype> newJobTitle, JobPrototype? newJobProto)
    {
        if (!TryComp<StationRecordKeyStorageComponent>(targetId, out var keyStorage)
            || keyStorage.Key is not { } key
            || !_record.TryGetRecord<GeneralStationRecord>(key, out var record))
        {
            return;
        }

        record.Name = newFullName;
        record.JobTitle = newJobTitle;

        if (newJobProto != null)
        {
            record.JobPrototype = newJobProto.ID;
            record.JobIcon = newJobProto.Icon;
        }

        _record.Synchronize(key);
    }

    private void OnMachineDeconstructed(Entity<IdCardConsoleComponent> entity, ref MachineDeconstructedEvent args)
    {
        TryDropAndThrowIds(entity.AsNullable());
    }

    private void OnDamageChanged(Entity<IdCardConsoleComponent> entity, ref DamageChangedEvent args)
    {
        if (TryDropAndThrowIds(entity.AsNullable()))
            _chat.TrySendInGameICMessage(entity, Loc.GetString("id-card-console-damaged"), InGameICChatType.Speak, true);
    }

    #region PublicAPI

    /// <summary>
    ///     Tries to drop any IDs stored in the console, and then tries to throw them away.
    ///     Returns true if anything was ejected and false otherwise.
    /// </summary>
    public bool TryDropAndThrowIds(Entity<IdCardConsoleComponent?, ItemSlotsComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp1, ref ent.Comp2))
            return false;

        var didEject = false;

        foreach (var slot in ent.Comp2.Slots.Values)
        {
            if (slot.Item == null || slot.ContainerSlot == null)
                continue;

            var item = slot.Item.Value;
            if (_container.Remove(item, slot.ContainerSlot))
            {
                _throwing.TryThrow(item, _random.NextVector2(), baseThrowSpeed: 5f);
                didEject = true;
            }
        }

        return didEject;
    }

    #endregion
}
