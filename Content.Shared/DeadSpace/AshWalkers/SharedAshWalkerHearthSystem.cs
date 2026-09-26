using Content.Shared.Audio;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;

namespace Content.Shared.DeadSpace.AshWalkers;

public abstract class SharedAshWalkerHearthSystem : EntitySystem
{
    [Dependency] private readonly SharedStackSystem _stacks = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPointLightSystem _lights = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AshWalkerHearthComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AshWalkerHearthComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<AshWalkerHearthComponent, ExaminedEvent>(OnExamined);
    }

    private void OnMapInit(Entity<AshWalkerHearthComponent> ent, ref MapInitEvent args)
    {
        SetBurning(ent, ent.Comp.FuelRemaining > 0 && Transform(ent).Anchored);
    }

    private void OnInteractUsing(Entity<AshWalkerHearthComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<StackComponent>(args.Used, out var stack) || stack.StackTypeId != ent.Comp.Fuel)
            return;

        args.Handled = true;
        if (ent.Comp.FuelRemaining + ent.Comp.FuelPerItem > ent.Comp.MaxFuel)
        {
            _popup.PopupClient(Loc.GetString("ash-walker-hearth-full"), ent, args.User);
            return;
        }

        if (!_stacks.TryUse((args.Used, stack), 1))
            return;

        ent.Comp.FuelRemaining += ent.Comp.FuelPerItem;
        DirtyField(ent.AsNullable(), nameof(ent.Comp.FuelRemaining));
        SetBurning(ent, Transform(ent).Anchored);
        _popup.PopupClient(Loc.GetString("ash-walker-hearth-fed"), ent, args.User);
    }

    private void OnExamined(Entity<AshWalkerHearthComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("ash-walker-hearth-fuel", ("seconds", (int) MathF.Ceiling(ent.Comp.FuelRemaining))));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<AshWalkerHearthComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var hearth, out var xform))
        {
            var active = hearth.FuelRemaining > 0 && xform.Anchored;
            if (hearth.Burning != active)
                SetBurning((uid, hearth), active);

            if (!active)
                continue;

            var elapsed = MathF.Min(frameTime, hearth.FuelRemaining);
            hearth.FuelRemaining -= elapsed;
            DirtyField(uid, hearth, nameof(hearth.FuelRemaining));
            HeatItems((uid, hearth), elapsed);

            if (hearth.FuelRemaining <= 0)
                SetBurning((uid, hearth), false);
        }
    }

    protected virtual void HeatItems(Entity<AshWalkerHearthComponent> ent, float elapsed)
    {
    }

    private void SetBurning(Entity<AshWalkerHearthComponent> ent, bool burning)
    {
        ent.Comp.Burning = burning;
        _appearance.SetData(ent, AshWalkerHearthVisuals.Burning, burning);
        _lights.SetEnabled(ent, burning);
        _ambient.SetAmbience(ent, burning);
    }
}
