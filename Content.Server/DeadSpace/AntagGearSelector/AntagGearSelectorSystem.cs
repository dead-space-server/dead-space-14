// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.EUI;
using Content.Server.Station.Systems;
using Content.Server.RandomMetadata;
using Content.Server.GameTicking.Rules;
using Content.Server.Humanoid;
using Content.Server.Mind;
using Content.Server.Preferences.Managers;
using Content.Shared.Clothing;
using Content.Shared.DeadSpace.AntagGearSelector;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.AntagGearSelector;

public sealed class AntagGearSelectorSystem : EntitySystem
{
    [Dependency] private readonly EuiManager _eui = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;
    [Dependency] private readonly LoadoutSystem _loadout = default!;
    [Dependency] private readonly AntagSelectionSystem _antagSelection = default!;
    [Dependency] private readonly RandomMetadataSystem _randomMetadata = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly IServerPreferencesManager _preferences = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private readonly HashSet<EntityUid> _selected = new();
    private readonly Dictionary<EntityUid, PendingSelection> _pending = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AntagGearSelectorComponent, AntagSelectEntityEvent>(OnSelectEntity,
            before: [typeof(AntagLoadProfileRuleSystem)]);
        SubscribeLocalEvent<AntagGearSelectorComponent, AfterAntagEntitySelectedEvent>(OnAntagSelected);
    }

    private void OnSelectEntity(Entity<AntagGearSelectorComponent> ent, ref AntagSelectEntityEvent args)
    {
        if (args.Handled || args.Session == null || !args.AntagRoles.Any(ent.Comp.Roles.Contains))
            return;

        // This controller has no sprite or body. AntagSelection places it at the role spawn marker.
        args.Entity = Spawn(null);
    }

    private void OnAntagSelected(Entity<AntagGearSelectorComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        var session = args.Session;
        if (session == null || _selected.Contains(args.EntityUid) ||
            !args.Def.PrefRoles.Any(ent.Comp.Roles.Contains))
            return;

        var deadline = _timing.CurTime + ent.Comp.SelectionTimeout;
        _pending[args.EntityUid] = new PendingSelection(
            session,
            ent.Owner,
            args.Def,
            deadline);
        var state = new AntagGearSelectorEuiState(ToOptions(ent.Comp.Gear), deadline);
        _eui.OpenEui(new AntagGearSelectorEui(this, session, args.EntityUid, ent, state), session);
    }

    public AntagGearSelectorEuiState GetState(EntityUid rule)
    {
        if (!TryComp<AntagGearSelectorComponent>(rule, out var selector))
            return new([], _timing.CurTime);

        return new(ToOptions(selector.Gear), _timing.CurTime + selector.SelectionTimeout);
    }

    private List<AntagGearSelectorOption> ToOptions(List<AntagGearSelectorEntry> entries)
    {
        var result = new List<AntagGearSelectorOption>(entries.Count);
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var option = new AntagGearSelectorOption(i, Loc.GetString(entry.Name), Loc.GetString(entry.Description), entry.SpritePrototype.Id)
            {
                Perks = ToPerkOptions(entry.Perks),
            };
            result.Add(option);
        }

        return result;
    }

    private List<AntagGearSelectorPerkOption> ToPerkOptions(List<AntagGearSelectorEntry> entries)
    {
        var result = new List<AntagGearSelectorPerkOption>(entries.Count);
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            result.Add(new AntagGearSelectorPerkOption(
                i,
                Loc.GetString(entry.Name),
                Loc.GetString(entry.Description),
                entry.SpritePrototype.Id));
        }

        return result;
    }

    public bool TryApplySelection(ICommonSession session, EntityUid target, EntityUid rule, int gearIndex, int perkIndex)
    {
        if (_selected.Contains(target) || session.AttachedEntity != target ||
            !TryComp<AntagGearSelectorComponent>(rule, out var selector) ||
            gearIndex < 0 || gearIndex >= selector.Gear.Count ||
            (selector.Gear[gearIndex].Perks.Count > 0 &&
                (perkIndex < 0 || perkIndex >= selector.Gear[gearIndex].Perks.Count)))
            return false;

        if (!_pending.Remove(target, out var pending) || !TrySpawnBody(target, pending, out var body))
            return false;

        var gear = selector.Gear[gearIndex];
        ApplyEntry(body, gear, session);
        if (gear.Perks.Count > 0)
            ApplyEntry(body, gear.Perks[perkIndex], session);
        if (gear.Briefing is { } briefing)
            _antagSelection.SendBriefing(session, briefing);
        _selected.Add(target);
        _selected.Add(body);
        QueueDel(target);
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        foreach (var (target, pending) in _pending.ToArray())
        {
            if (now < pending.Deadline)
                continue;

            if (!Exists(target) || !TryComp<AntagGearSelectorComponent>(pending.Rule, out var selector) ||
                selector.Gear.Count == 0)
            {
                _pending.Remove(target);
                continue;
            }

            var gearIndex = _random.Next(selector.Gear.Count);
            var perks = selector.Gear[gearIndex].Perks;
            var perkIndex = perks.Count == 0 ? -1 : _random.Next(perks.Count);
            TryApplySelection(pending.Session, target, pending.Rule, gearIndex, perkIndex);
        }
    }

    private bool TrySpawnBody(EntityUid controller, PendingSelection pending, out EntityUid body)
    {
        body = default;
        if (!_transform.TryGetMapOrGridCoordinates(controller, out var coordinates) ||
            !_mind.TryGetMind(controller, out var mindId, out var mind))
            return false;

        var profile = _preferences.GetPreferences(pending.Session.UserId).SelectedCharacter as HumanoidCharacterProfile;
        profile ??= HumanoidCharacterProfile.RandomWithSpecies("Human");
        profile = profile.WithSpecies("Human");

        body = Spawn("MobHuman", coordinates.Value);
        _humanoid.LoadProfile(body, profile);
        EntityManager.AddComponents(body, pending.Definition.Components);
        _mind.TransferTo(mindId, body, true, mind: mind);
        return true;
    }

    private void ApplyEntry(EntityUid target, AntagGearSelectorEntry entry, ICommonSession session)
    {
        EntityManager.AddComponents(target, entry.Components);
        if (TryComp<RandomMetadataComponent>(target, out var random))
        {
            if (random.NameSegments != null)
                _metadata.SetEntityName(target, _randomMetadata.GetRandomFromSegments(random.NameSegments, random.NameFormat));
            if (random.DescriptionSegments != null)
                _metadata.SetEntityDescription(target,
                    _randomMetadata.GetRandomFromSegments(random.DescriptionSegments, random.DescriptionFormat));
        }
        if (entry.StartingGear is { } gear)
            _stationSpawning.EquipStartingGear(target, gear, raiseEvent: false);

        if (entry.RoleLoadout is not { } roleLoadout ||
            !_prototypes.TryIndex(roleLoadout, out RoleLoadoutPrototype? roleLoadoutPrototype))
            return;

        var profile = _preferences.GetPreferences(session.UserId).SelectedCharacter as HumanoidCharacterProfile;
        if (profile != null && profile.Loadouts.TryGetValue(roleLoadout, out var selectedLoadout))
            _loadout.Equip(target, null, selectedLoadout, roleLoadoutPrototype);
        else
            _loadout.Equip(target, null, [roleLoadout]);
    }

    private sealed record PendingSelection(
        ICommonSession Session,
        EntityUid Rule,
        AntagSelectionDefinition Definition,
        TimeSpan Deadline);
}
