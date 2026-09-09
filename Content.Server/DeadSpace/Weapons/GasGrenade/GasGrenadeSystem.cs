// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Atmos.EntitySystems;
using Content.Shared.Actions;
using Content.Shared.Atmos.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeadSpace.Weapons.GasGrenade;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Toggleable;
using Content.Shared.Trigger;
using Content.Shared.Verbs;
using JetBrains.Annotations;
using Robust.Shared.Audio.Systems;

namespace Content.Server.DeadSpace.Weapons.GasGrenade;

[UsedImplicitly]
public sealed class GasGrenadeSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<GasGrenadeShellComponent, GetItemActionsEvent>(OnShellGetActions,
            after: new[] { typeof(GasTankSystem) });
        SubscribeLocalEvent<GasGrenadeShellComponent, ToggleActionEvent>(OnShellToggleInternals,
            before: new[] { typeof(GasTankSystem) });

        SubscribeLocalEvent<GasGrenadeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<GasGrenadeComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<GasGrenadeComponent, AttemptTriggerEvent>(OnAttemptTrigger);
        SubscribeLocalEvent<GasGrenadeComponent, TriggerEvent>(OnTrigger);
        SubscribeLocalEvent<GasGrenadeComponent, ExaminedEvent>(OnExamined);
    }

    private void OnMapInit(Entity<GasGrenadeComponent> ent, ref MapInitEvent args)
    {
        UpdateAppearance(ent);
    }

    private void OnGetVerbs(Entity<GasGrenadeComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var category = new VerbCategory(Loc.GetString("gas-grenade-verb-category"), null);
        var user = args.User;

        foreach (var mode in new[] { GasGrenadeMode.Mix, GasGrenadeMode.Spray })
        {
            var target = mode;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString(target == GasGrenadeMode.Mix
                    ? "gas-grenade-mode-mix"
                    : "gas-grenade-mode-spray"),
                Category = category,
                Disabled = ent.Comp.Mode == target,
                Act = () => SetMode(ent, target, user),
            });
        }
    }

    private void SetMode(Entity<GasGrenadeComponent> ent, GasGrenadeMode mode, EntityUid user)
    {
        ent.Comp.Mode = mode;
        UpdateAppearance(ent);
        _popup.PopupEntity(Loc.GetString(mode == GasGrenadeMode.Mix
            ? "gas-grenade-mode-mix"
            : "gas-grenade-mode-spray"), ent, user);
    }

    private void UpdateAppearance(Entity<GasGrenadeComponent> ent)
    {
        GasGrenadeVisualState state;
        if (ent.Comp.Releasing)
            state = GasGrenadeVisualState.Releasing;
        else
            state = ent.Comp.Mode == GasGrenadeMode.Mix
                ? GasGrenadeVisualState.MixIdle
                : GasGrenadeVisualState.SprayIdle;

        _appearance.SetData(ent.Owner, GasGrenadeVisuals.State, state);
    }

    private void OnShellGetActions(EntityUid uid, GasGrenadeShellComponent comp, GetItemActionsEvent args)
    {
        if (TryComp<GasTankComponent>(uid, out var tank) && tank.ToggleActionEntity is { } action)
            args.Actions.Remove(action);
    }

    private void OnShellToggleInternals(Entity<GasGrenadeShellComponent> ent, ref ToggleActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("gas-grenade-shell-no-internals"), ent, args.Performer);
    }

    private void OnAttemptTrigger(Entity<GasGrenadeComponent> ent, ref AttemptTriggerEvent args)
    {
        if (GetShellTanks(ent).Count >= ent.Comp.SlotIds.Count)
            return;

        args.Cancelled = true;
        if (args.User is { } user)
            _popup.PopupEntity(Loc.GetString("gas-grenade-needs-shells"), ent, user);
    }

    private void OnTrigger(Entity<GasGrenadeComponent> ent, ref TriggerEvent args)
    {
        if (args.Key == null || !ent.Comp.KeysIn.Contains(args.Key))
            return;

        if (ent.Comp.MixCountdown != null)
            return;

        var shells = GetShellTanks(ent);
        if (shells.Count == 0)
        {
            args.Handled = true;
            return;
        }

        ent.Comp.Releasing = true;
        ent.Comp.MixCountdown = ent.Comp.MixDelay;
        UpdateAppearance(ent);

        if (ent.Comp.Mode == GasGrenadeMode.Mix)
        {
            MixShells(shells);
            ent.Comp.MixReactTimer = ent.Comp.MixReactInterval;
            ent.Comp.MixShellEntity = shells[0].Owner;
        }
        else
        {
            ReleaseAll(ent, shells);
        }

        args.Handled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<GasGrenadeComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.MixCountdown == null)
                continue;

            if (comp.Mode == GasGrenadeMode.Mix)
            {
                comp.MixReactTimer -= frameTime;
                if (comp.MixReactTimer <= 0f)
                {
                    if (comp.MixShellEntity is { } shellUid && TryComp<GasTankComponent>(shellUid, out var tank))
                        _atmos.React(tank.Air, tank);

                    comp.MixReactTimer = comp.MixReactInterval;
                }
            }

            comp.MixCountdown -= frameTime;
            if (comp.MixCountdown > 0f)
                continue;

            comp.MixCountdown = null;
            comp.MixShellEntity = null;


            if (comp.Mode == GasGrenadeMode.Mix)
                ReleaseAll((uid, comp), GetShellTanks((uid, comp)));

            comp.Releasing = false;
            UpdateAppearance((uid, comp));
        }
    }

    private void MixShells(List<Entity<GasTankComponent>> shells)
    {
        var container = shells[0].Comp;
        var target = container.Air;
        for (var i = 1; i < shells.Count; i++)
        {
            _atmos.Merge(target, shells[i].Comp.Air);
            shells[i].Comp.Air.Clear();
        }

        _atmos.React(target, container);
    }

    private void ReleaseAll(Entity<GasGrenadeComponent> ent, List<Entity<GasTankComponent>> shells)
    {
        var env = _atmos.GetContainingMixture(ent.Owner, false, true);

        foreach (var (_, tank) in shells)
        {
            if (env != null)
                _atmos.Merge(env, tank.Air);

            tank.Air.Clear();
        }

        _audio.PlayPvs(ent.Comp.ReleaseSound, ent.Owner);
    }

    private List<Entity<GasTankComponent>> GetShellTanks(Entity<GasGrenadeComponent> ent)
    {
        var list = new List<Entity<GasTankComponent>>();
        foreach (var slotId in ent.Comp.SlotIds)
        {
            if (_itemSlots.GetItemOrNull(ent.Owner, slotId) is { } item && TryComp<GasTankComponent>(item, out var tank))
                list.Add((item, tank));
        }

        return list;
    }

    private void OnExamined(Entity<GasGrenadeComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("gas-grenade-examine-mode",
            ("mode", Loc.GetString(ent.Comp.Mode == GasGrenadeMode.Mix
                ? "gas-grenade-mode-mix"
                : "gas-grenade-mode-spray"))));
    }
}
