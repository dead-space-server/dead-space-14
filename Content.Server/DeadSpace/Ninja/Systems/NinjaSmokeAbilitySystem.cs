// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Fluids.EntitySystems;
using Content.Server.Spreader;
using Content.Shared.Actions;
using Content.Shared.Chemistry.Components;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Content.Shared.DeadSpace.Ninja.Systems;
using Content.Shared.Ninja.Systems;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class NinjaSmokeAbilitySystem : SharedNinjaSmokeAbilitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SmokeSystem _smoke = default!;
    [Dependency] private readonly SpreaderSystem _spreader = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedSpaceNinjaSystem _ninja = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaSmokeAbilityComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NinjaSmokeAbilityComponent, GetItemActionsEvent>(OnGetActions);

        SubscribeLocalEvent<NinjaSmokeAbilityComponent, SpiderOSPowerChangedEvent>(OnSpiderOSPowerChanged);

        SubscribeLocalEvent<NinjaSmokeAbilityComponent, NinjaSmokeAbilityActionEvent>(OnSmokeAction);
        SubscribeLocalEvent<NinjaSmokeAbilityComponent, NinjaToggleAutoSmokeActionEvent>(OnSmokeAutoModeToggleAction);
    }

    private void OnMapInit(Entity<NinjaSmokeAbilityComponent> ent, ref MapInitEvent args)
    {
        var (uid, comp) = ent;
        _actions.AddAction(uid, ref comp.ActionSmokeEntity, comp.ActionSmoke);
        _actions.AddAction(uid, ref comp.ActionAutoSmokeEntity, comp.ActionAutoSmoke);
        Dirty(uid, comp);
    }

    private void OnGetActions(Entity<NinjaSmokeAbilityComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands)
            return;

        if (!TryComp<SpiderOSComponent>(ent.Owner, out var os) || !os.SuitActivated)
            return;

        args.AddAction(ent.Comp.ActionSmokeEntity);
        args.AddAction(ent.Comp.ActionAutoSmokeEntity);
    }

    private void OnSpiderOSPowerChanged(Entity<NinjaSmokeAbilityComponent> ent, ref SpiderOSPowerChangedEvent args)
    {
        if (!args.Activated)
        {
            _actions.RemoveAction(ent.Comp.ActionSmokeEntity);
            _actions.RemoveAction(ent.Comp.ActionAutoSmokeEntity);
        }
    }

    private void OnSmokeAction(Entity<NinjaSmokeAbilityComponent> ent, ref NinjaSmokeAbilityActionEvent args)
    {
        args.Handled = TrySpawnNinjaSmoke(ent, false);
    }

    private void OnSmokeAutoModeToggleAction(Entity<NinjaSmokeAbilityComponent> ent, ref NinjaToggleAutoSmokeActionEvent args)
    {
        args.Handled = true;
        var (uid, comp) = ent;
        comp.AutoMode = !comp.AutoMode;
        Dirty(uid, comp);
        _actions.SetToggled(comp.ActionAutoSmokeEntity, comp.AutoMode);
    }

    public override bool TrySpawnNinjaSmoke(Entity<NinjaSmokeAbilityComponent> ent, bool autoMode)
    {
        if (autoMode && !ent.Comp.AutoMode)
            return false;

        var user = Transform(ent).ParentUid;
        if (!user.IsValid())
            return false;

        float energyCost = autoMode ? ent.Comp.EnergyCostAutoMode : ent.Comp.EnergyCost;

        var xform = Transform(user);
        var mapCoords = _xform.GetMapCoordinates(user);
        if (!_mapManager.TryFindGridAt(mapCoords, out var gridUid, out var grid) ||
            !_map.TryGetTileRef(gridUid, grid, xform.Coordinates, out var tileRef))
            return false;

        if (_spreader.RequiresFloorToSpread(ent.Comp.SmokePrototype.ToString()) && _turf.IsSpace(tileRef))
            return false;

        if (!_ninja.TryUseCharge(user, energyCost))
        {
            if (!autoMode)
                _popup.PopupEntity(Loc.GetString("ninja-no-power"), user, user);
            return false;
        }

        var coords = _map.MapToGrid(gridUid, mapCoords);
        var smoke = Spawn(ent.Comp.SmokePrototype, coords.SnapToGrid());
        if (!TryComp<SmokeComponent>(smoke, out var smokeComp))
        {
            QueueDel(smoke);
            return false;
        }

        _audio.PlayPvs(ent.Comp.SmokeSound, user);
        if (!autoMode)
            _smoke.StartSmoke(smoke, new Solution(), (float)ent.Comp.Duration.TotalSeconds, ent.Comp.SpreadAmount, smokeComp);
        else
            _smoke.StartSmoke(smoke, new Solution(), (float)ent.Comp.DurationAutoMode.TotalSeconds, ent.Comp.SpreadAmountAutoMode, smokeComp);
        return true;
    }
}