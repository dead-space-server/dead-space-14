// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Humanoid;
using Content.Client.Inventory;
using Content.Client.Station;
using Content.Shared.Chat.TypingIndicator;
using Content.Shared.Clothing;
using Content.Shared.DeadSpace.Storyteller;
using Content.Shared.GameTicking;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Storyteller;

public sealed class StorytellerSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private ClientInventorySystem _inventory = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private StationSpawningSystem _spawning = default!;
    [Dependency] private SpriteSystem _sprites = default!;

    private readonly Dictionary<byte, (ushort Look, EntityUid? Doll)> _dolls = new();
    private readonly Dictionary<byte, (string Id, EntityUid? Item)> _held = new();
    private StorytellerWindow? _window;
    private bool _closing;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<StorySceneEvent>(OnScene);
        SubscribeNetworkEvent<StorySuggestionsEvent>(OnSuggestions);
        SubscribeNetworkEvent<StoryClosedEvent>(_ => CloseLocal());
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(_ => CloseLocal());
        _overlays.AddOverlay(new StorytellerOverlay(EntityManager, _prototypes, _timing));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<StorytellerOverlay>();
        CloseLocal();
    }

    public EntityUid? Doll(byte id)
    {
        return _dolls.TryGetValue(id, out var doll) ? doll.Doll : null;
    }

    public bool InHand(byte id)
    {
        return _held.TryGetValue(id, out var held) && held.Item is { } item && !Deleted(item);
    }

    private void SyncHeld(StoryActorState actor)
    {
        var doll = Doll(actor.Id);
        if (_held.TryGetValue(actor.Id, out var current) && current.Id == actor.Held &&
            (current.Item == null || !Deleted(current.Item.Value)))
        {
            return;
        }

        if (current.Item is { } old && !Deleted(old))
            Del(old);

        _held.Remove(actor.Id);
        if (doll == null || actor.Held.Length == 0 || !_prototypes.TryIndex<StoryItemPrototype>(actor.Held, out var proto))
        {
            _held[actor.Id] = (actor.Held, null);
            return;
        }

        EntityUid? item = null;
        try
        {
            item = Spawn(proto.Item, MapCoordinates.Nullspace);
            if (!_hands.TryPickupAnyHand(doll.Value, item.Value, checkActionBlocker: false, animate: false))
            {
                Del(item.Value);
                item = null;
            }
        }
        catch (Exception e)
        {
            Log.Warning($"Unable to put {proto.Item} into a story doll's hand: {e.Message}");
            if (item != null && !Deleted(item.Value))
                Del(item.Value);
            item = null;
        }

        _held[actor.Id] = (actor.Held, item);
    }

    private void OnScene(StorySceneEvent ev)
    {
        var teller = _player.LocalEntity is { } local && GetNetEntity(local) == ev.Teller;
        SyncDolls(ev.Actors);

        if (_window == null)
        {
            var window = new StorytellerWindow(this, EntityManager, _prototypes);
            window.OnClose += OnWindowClosed;
            window.OpenCentered();
            _window = window;
        }

        _window.SetTeller(teller);
        _window.SetScene(ev);
    }

    private void OnSuggestions(StorySuggestionsEvent ev)
    {
        if (ev.Labels.Length != ev.Cues.Length || ev.Labels.Length > StorytellerParser.MaxCues)
            return;

        _window?.ShowSuggestions(ev.Labels, ev.Cues);
    }

    public void SendLine(string line, bool apply)
    {
        RaiseNetworkEvent(new StoryLineRequest { Line = line, Apply = apply });
    }

    public void SendCues(params StoryCue[] cues)
    {
        if (cues.Length > 0)
            RaiseNetworkEvent(new StoryCuesRequest { Cues = cues });
    }

    public void SendWriteBook(string title)
    {
        RaiseNetworkEvent(new StoryWriteBookRequest { Title = title });
    }

    public void SendPage(bool restart, bool back = false)
    {
        RaiseNetworkEvent(new StoryPageRequest { Restart = restart, Back = back });
    }

    private void OnWindowClosed()
    {
        if (_closing)
            return;

        var teller = _window?.Teller ?? false;
        _window = null;
        ClearDolls();
        if (teller)
            RaiseNetworkEvent(new StoryStopRequest());
        else
            RaiseNetworkEvent(new StoryLeaveRequest());
    }

    private void CloseLocal()
    {
        if (_window != null)
        {
            _closing = true;
            _window.Close();
            _window = null;
            _closing = false;
        }

        ClearDolls();
    }

    private void SyncDolls(StoryActorState[] actors)
    {
        var seen = new HashSet<byte>();
        foreach (var actor in actors)
        {
            seen.Add(actor.Id);
            if (!_dolls.TryGetValue(actor.Id, out var existing) || existing.Look != actor.Look)
            {
                if (existing.Doll is { } old && !Deleted(old))
                    Del(old);

                _held.Remove(actor.Id);
                _dolls[actor.Id] = (actor.Look, BuildDoll(actor));
            }

            SyncHeld(actor);
        }

        foreach (var id in new List<byte>(_dolls.Keys))
        {
            if (seen.Contains(id))
                continue;

            if (_dolls[id].Doll is { } doll && !Deleted(doll))
                Del(doll);
            _dolls.Remove(id);
            _held.Remove(id);
        }
    }

    private void ClearDolls()
    {
        foreach (var (_, doll) in _dolls.Values)
        {
            if (doll is { } uid && !Deleted(uid))
                Del(uid);
        }

        _dolls.Clear();
        _held.Clear();
    }

    private EntityUid? BuildDoll(StoryActorState actor)
    {
        SpeciesPrototype? species = null;
        EntProtoId? body = null;

        if (actor.Copy?.Humanoid is { } copied && _prototypes.TryIndex(copied.Species, out species))
            body = species.DollPrototype;
        else if (actor.Copy?.BodyPrototype is { } copiedBody && _prototypes.HasIndex<EntityPrototype>(copiedBody))
            body = copiedBody;
        else if (_prototypes.TryIndex<StoryKindPrototype>(actor.Kind, out var kind))
        {
            if (kind.Species is { } speciesId && _prototypes.TryIndex(speciesId, out species))
                body = species.DollPrototype;
            else if (kind.Mob is { } mob)
                body = mob;
        }

        if (body == null || !_prototypes.HasIndex<EntityPrototype>(body.Value))
            return null;

        EntityUid uid;
        try
        {
            uid = Spawn(body.Value, MapCoordinates.Nullspace);
        }
        catch (Exception e)
        {
            Log.Warning($"Unable to spawn story doll {body}: {e.Message}");
            return null;
        }

        if (!TryComp<SpriteComponent>(uid, out var sprite))
        {
            Del(uid);
            return null;
        }

        try
        {
            HumanoidCharacterProfile? profile = null;
            if (species != null)
            {
                var gender = actor.Sex switch
                {
                    Sex.Female => Gender.Female,
                    Sex.Male => Gender.Male,
                    _ => Gender.Epicene,
                };
                profile = HumanoidCharacterProfile.DefaultWithSpecies(species.ID).WithSex(actor.Sex).WithGender(gender);
                if (actor.Appearance != null)
                    profile.Appearance = actor.Appearance;
            }

            if (HasComp<HumanoidAppearanceComponent>(uid))
            {
                if (actor.Copy?.Humanoid is { } look)
                    _humanoid.ApplyRoundEndAppearance(uid, look);
                else if (actor.Appearance != null && profile != null)
                    _humanoid.LoadProfile(uid, profile);
            }

            if (actor.Copy is { Equipment.Length: > 0 } copy)
            {
                foreach (var entry in copy.Equipment)
                    Equip(uid, entry.Slot, entry.Prototype);
            }
            else if (actor.Role.Length > 0 && _prototypes.TryIndex<StoryRolePrototype>(actor.Role, out var role))
            {
                Dress(uid, role, profile);
            }

            if (TryComp<HandsComponent>(uid, out var hands) && hands.Hands.Count == 0)
            {
                _hands.AddHand((uid, hands), "story-right", HandLocation.Right);
                _hands.AddHand((uid, hands), "story-left", HandLocation.Left);
            }

            if (_sprites.LayerMapTryGet((uid, sprite), TypingIndicatorLayers.Base, out var layer, false))
                _sprites.LayerSetVisible((uid, sprite), layer, false);

            _sprites.SetRotation((uid, sprite), Angle.Zero);
            _sprites.ForceUpdate(uid);
        }
        catch (Exception e)
        {
            Log.Warning($"Unable to dress story doll {body}: {e.Message}");
        }

        return uid;
    }

    private void Dress(EntityUid body, StoryRolePrototype role, HumanoidCharacterProfile? profile)
    {
        if (role.Gear is { } gear)
            _spawning.EquipStartingGear(body, gear, false);

        if (role.Job is not { } jobId || !_prototypes.TryIndex(jobId, out JobPrototype? job))
            return;

        _spawning.EquipStartingGear(body, job.StartingGear, false);

        var loadoutId = LoadoutSystem.GetJobPrototype(job.ID);
        if (profile == null || !_prototypes.HasIndex<RoleLoadoutPrototype>(loadoutId))
            return;

        var loadout = new RoleLoadout(loadoutId);
        loadout.SetDefault(profile, _player.LocalSession, _prototypes, force: true);
        foreach (var group in loadout.SelectedLoadouts.Values)
        {
            foreach (var entry in group)
            {
                if (_prototypes.Resolve(entry.Prototype, out var proto))
                    _spawning.EquipStartingGear(body, proto, false);
            }
        }
    }

    private void Equip(EntityUid body, string slot, string prototype)
    {
        if (!_prototypes.HasIndex<EntityPrototype>(prototype))
            return;

        EntityUid? item = null;
        try
        {
            item = Spawn(prototype, MapCoordinates.Nullspace);
            if (!_inventory.TryEquip(body, item.Value, slot, true, true))
                Del(item.Value);
        }
        catch (Exception e)
        {
            Log.Warning($"Unable to equip story doll item {prototype}: {e.Message}");
            if (item != null && !Deleted(item.Value))
                Del(item.Value);
        }
    }
}
