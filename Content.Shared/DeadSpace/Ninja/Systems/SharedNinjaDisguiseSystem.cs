// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Diagnostics.CodeAnalysis;
using Content.Shared.Actions;
using Content.Shared.DeadSpace.Ninja.Components;
using Robust.Shared.Containers;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Inventory;

namespace Content.Shared.DeadSpace.Ninja.Systems;

public abstract class SharedNinjaDisguiseSystem : EntitySystem
{
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoidAppearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaDisguiseComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NinjaDisguiseComponent, GetItemActionsEvent>(OnGetItemActions);

        SubscribeLocalEvent<NinjaDisguiseComponent, SpiderOSPowerChangedEvent>(OnSpiderOSPowerChanged);
    }

    private void OnMapInit(Entity<NinjaDisguiseComponent> ent, ref MapInitEvent args)
    {
        var (uid, comp) = ent;

        if (!TryComp<SpiderOSComponent>(ent.Owner, out var os) || !os.SuitActivated)
            return;

        _actionContainer.EnsureAction(uid, ref comp.ActionScanEntity, comp.ActionScan);
        _actionContainer.EnsureAction(uid, ref comp.ActionMenuEntity, comp.ActionMenu);
        Dirty(uid, comp);
    }

    private void OnSpiderOSPowerChanged(Entity<NinjaDisguiseComponent> ent, ref SpiderOSPowerChangedEvent args)
    {
        if (!args.Activated)
        {
            _actions.RemoveAction(ent.Comp.ActionScanEntity);
            _actions.RemoveAction(ent.Comp.ActionMenuEntity);
            var revealed = new NinjaDisguiseRevealedEvent();
            RaiseLocalEvent(ent, ref revealed);
        }
    }

    private void OnGetItemActions(Entity<NinjaDisguiseComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands)
            return;

        if (!TryComp<SpiderOSComponent>(ent.Owner, out var os) || !os.SuitActivated)
            return;

        if (ent.Comp.ActionScanEntity == null || ent.Comp.ActionMenuEntity == null)
            return;

        args.AddAction(ent.Comp.ActionScanEntity);
        args.AddAction(ent.Comp.ActionMenuEntity);
    }

    public bool TryGetActiveDisguise(EntityUid wearer, [NotNullWhen(true)] out EntityUid suitUid,
        [NotNullWhen(true)] out NinjaDisguiseComponent? comp)
    {
        suitUid = EntityUid.Invalid;
        comp = null!;

        if (!TryComp<ContainerManagerComponent>(wearer, out var containers))
            return false;

        foreach (var container in containers.Containers.Values)
        {
            if (container.Count != 1)
                continue;

            var item = container.ContainedEntities[0];
            if (!item.IsValid() || !TryComp(item, out comp) || !comp.Disguised)
                continue;

            suitUid = item;
            return true;
        }

        return false;
    }

    public static NinjaDisguiseAppearance CaptureAppearance(HumanoidAppearanceComponent humanoid)
    {
        return new NinjaDisguiseAppearance
        {
            MarkingSet = new MarkingSet(humanoid.MarkingSet),
            Species = humanoid.Species,
            SkinColor = humanoid.SkinColor,
            EyeColor = humanoid.EyeColor,
            SpeakerColor = humanoid.SpeakerColor,
            Sex = humanoid.Sex,
            Gender = humanoid.Gender,
            Age = humanoid.Age,
            Voice = humanoid.Voice,
            PermanentlyHidden = new HashSet<HumanoidVisualLayers>(humanoid.PermanentlyHidden),
            CustomBaseLayers = new Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo>(humanoid.CustomBaseLayers),
            HairGradientEnabled = humanoid.HairGradientEnabled,
            HairGradientColor = humanoid.HairGradientColor,
        };
    }

    public void ApplyAppearance(Entity<HumanoidAppearanceComponent> humanoid, NinjaDisguiseAppearance saved)
    {
        humanoid.Comp.MarkingSet = new MarkingSet(saved.MarkingSet);

        if (saved.Species.Id != null)
            humanoid.Comp.Species = saved.Species.Id;

        humanoid.Comp.SkinColor = saved.SkinColor;
        humanoid.Comp.EyeColor = saved.EyeColor;
        humanoid.Comp.SpeakerColor = saved.SpeakerColor;
        _humanoidAppearance.SetSex(humanoid.Owner, saved.Sex);
        _humanoidAppearance.SetGender(humanoid.Owner, saved.Gender);
        humanoid.Comp.Age = saved.Age;
        _humanoidAppearance.SetTTSVoice(humanoid.Owner, saved.Voice, humanoid);
        humanoid.Comp.PermanentlyHidden = new HashSet<HumanoidVisualLayers>(saved.PermanentlyHidden);
        humanoid.Comp.CustomBaseLayers = new Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo>(saved.CustomBaseLayers);

        humanoid.Comp.HiddenLayers = new Dictionary<HumanoidVisualLayers, SlotFlags>();

        humanoid.Comp.HairGradientEnabled = saved.HairGradientEnabled;
        humanoid.Comp.HairGradientColor = saved.HairGradientColor;
    }
}