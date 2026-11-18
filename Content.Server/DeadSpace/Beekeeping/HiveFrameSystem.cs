// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Stack;
using Content.Shared.DeadSpace.Beekeeping;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Beekeeping;

/// <summary>
/// The frame of a hive: bees fill it with honeycomb, a knife cuts the honeycomb out (and ruins the frame).
/// </summary>
public sealed class HiveFrameSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> KnifeTag = "Knife";

    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HiveFrameComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<HiveFrameComponent, HiveFrameDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<HiveFrameComponent, ExaminedEvent>(OnExamined);
    }

    private void OnInteractUsing(Entity<HiveFrameComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tag.HasTag(args.Used, KnifeTag))
            return;

        // The knife does nothing else to a frame, whether it has honeycomb or not.
        args.Handled = true;

        if (ent.Comp.HoneycombAmount < 1f)
            return;

        var doAfter = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(2), new HiveFrameDoAfterEvent(), ent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnDoAfter(Entity<HiveFrameComponent> ent, ref HiveFrameDoAfterEvent args)
    {
        // The honeycomb could be gone by now.
        if (args.Cancelled || args.Handled || ent.Comp.HoneycombAmount < 1f)
            return;

        args.Handled = true;

        var honeycomb = Spawn(ent.Comp.Honeycomb, Transform(ent).Coordinates);
        _stack.SetCount(honeycomb, (int) MathF.Floor(ent.Comp.HoneycombAmount));
        _hands.PickupOrDrop(args.User, honeycomb);

        QueueDel(ent);
    }

    private void OnExamined(Entity<HiveFrameComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("hive-frame-examine",
            ("honeycomb", (int) ent.Comp.HoneycombAmount),
            ("max", (int) ent.Comp.MaxCapacity)));
    }

    public void UpdateVisuals(Entity<HiveFrameComponent> ent)
    {
        _appearance.SetData(ent, HiveFrameVisuals.HasHoneycomb, ent.Comp.HoneycombAmount >= 1f);
    }
}
