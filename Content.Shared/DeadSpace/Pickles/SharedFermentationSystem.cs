// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Pickles.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.Pickles;

public abstract class SharedFermentationSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] protected readonly SharedAppearanceSystem Appearance = default!;
    [Dependency] protected readonly SharedContainerSystem Container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FermentationBarrelComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<FermentationBarrelComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<FermentationBarrelComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        // Single subscription: client claims jar clicks so SolutionTransfer stays silent;
        // server Override handles insert/pack/fill.
        SubscribeLocalEvent<FermentationBarrelComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(Entity<FermentationBarrelComponent> ent, ref InteractUsingEvent args)
    {
        if (!IsEnabled())
            return;

        // Client: silence predicted SolutionTransfer/"empty" popups when using a jar on the barrel.
        if (_net.IsClient)
        {
            if (HasComp<PickleJarComponent>(args.Used))
                args.Handled = true;
            return;
        }

        // Server: jars must win over RefillableSolution/SolutionTransfer on the same entity.
        if (HasComp<PickleJarComponent>(args.Used))
        {
            HandleInteractUsing(ent, ref args);
            args.Handled = true;
            return;
        }

        if (args.Handled)
            return;

        HandleInteractUsing(ent, ref args);
    }

    protected virtual void HandleInteractUsing(Entity<FermentationBarrelComponent> ent, ref InteractUsingEvent args)
    {
    }

    public bool IsEnabled() => _cfg.GetCVar(PicklesCVars.Enabled);

    private void OnInit(Entity<FermentationBarrelComponent> ent, ref ComponentInit args)
    {
        Container.EnsureContainer<Container>(ent, FermentationBarrelComponent.ProduceContainerId);
        UpdateVisuals(ent);
    }

    protected void UpdateVisuals(Entity<FermentationBarrelComponent> ent)
    {
        Appearance.SetData(ent, FermentationVisuals.State, ent.Comp.State);
    }

    private void OnExamined(Entity<FermentationBarrelComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (!IsEnabled())
        {
            args.PushMarkup(Loc.GetString("pickle-barrel-disabled"));
            return;
        }

        args.PushMarkup(Loc.GetString($"pickle-barrel-state-{ent.Comp.State.ToString().ToLowerInvariant()}"));

        if (Container.TryGetContainer(ent, FermentationBarrelComponent.ProduceContainerId, out var container))
            args.PushMarkup(Loc.GetString("pickle-barrel-produce-count", ("count", container.ContainedEntities.Count), ("max", ent.Comp.MaxProduce)));

        if (ent.Comp.State == FermentationState.Fermenting && ent.Comp.TargetDuration > TimeSpan.Zero)
        {
            var pct = (int) Math.Clamp(ent.Comp.Elapsed / ent.Comp.TargetDuration * 100, 0, 100);
            args.PushMarkup(Loc.GetString("pickle-barrel-progress", ("percent", pct)));
        }

        if (ent.Comp.State == FermentationState.Ready && ent.Comp.ReadyMethod == PickleMethod.Alcohol)
            args.PushMarkup(Loc.GetString("pickle-barrel-ready-alcohol"));
    }

    private void OnGetVerbs(Entity<FermentationBarrelComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !IsEnabled())
            return;

        var user = args.User;

        if (ent.Comp.State == FermentationState.Idle)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("pickle-barrel-verb-start"),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/dot.svg.192dpi.png")),
                Act = () => TryStartFermentation(ent, user),
                Priority = 3
            });
        }

        if (ent.Comp.State is FermentationState.Idle or FermentationState.Ready)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("pickle-barrel-verb-tip"),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/spill.svg.192dpi.png")),
                Act = () => TipBarrel(ent, user),
                Priority = 0
            });
        }

        if (ent.Comp.State == FermentationState.Idle &&
            Container.TryGetContainer(ent, FermentationBarrelComponent.ProduceContainerId, out var container) &&
            container.ContainedEntities.Count > 0)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("pickle-barrel-verb-empty"),
                Act = () => EjectProduce(ent, user),
                Priority = 1
            });
        }
    }

    protected virtual bool TryStartFermentation(Entity<FermentationBarrelComponent> ent, EntityUid user) => false;
    protected virtual void EjectProduce(Entity<FermentationBarrelComponent> ent, EntityUid user) { }
    protected virtual void TipBarrel(Entity<FermentationBarrelComponent> ent, EntityUid user) { }

    public PickleRecipePrototype? FindRecipe(EntProtoId produce, PickleMethod method)
    {
        foreach (var recipe in _proto.EnumeratePrototypes<PickleRecipePrototype>())
        {
            if (recipe.Produce == produce && recipe.Method == method)
                return recipe;
        }

        return null;
    }

    public bool HasAnyRecipe(EntProtoId produce)
    {
        foreach (var recipe in _proto.EnumeratePrototypes<PickleRecipePrototype>())
        {
            if (recipe.Produce == produce)
                return true;
        }

        return false;
    }

    public static Color TintFor(PickleMethod method) => method switch
    {
        PickleMethod.Salt => Color.FromHex("#6a7a55"),
        PickleMethod.Alcohol => Color.FromHex("#c4a050"),
        _ => Color.FromHex("#e8d070"),
    };
}
