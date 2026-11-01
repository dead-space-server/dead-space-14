// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.Paper;
using Content.Shared.Body.Components;
using Content.Shared.Buckle.Components;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.CameraArchives;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Surgery;

public sealed class SurgeryXrayTableSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SurgerySystem _surgery = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    private static readonly SoundPathSpecifier PrintSound = new("/Audio/Machines/printer.ogg");

    private readonly Dictionary<EntityUid, TimeSpan> _next = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryXrayTableComponent, AfterActivatableUIOpenEvent>(OnOpen);
        SubscribeLocalEvent<SurgeryXrayTableComponent, SurgeryXrayTablePrintMessage>(OnPrint);
        SubscribeLocalEvent<SurgeryXrayTableComponent, SurgeryXrayTableStartMessage>(OnStart);
        SubscribeLocalEvent<SurgeryXrayTableComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<SurgeryXrayTableComponent, UnstrappedEvent>(OnUnstrapped);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<SurgeryXrayTableComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!_ui.IsUiOpen(uid, SurgeryXrayTableUiKey.Key))
                continue;

            if (_next.TryGetValue(uid, out var due) && _timing.CurTime < due)
                continue;

            _next[uid] = _timing.CurTime + TimeSpan.FromSeconds(1);
            _ui.SetUiState(uid, SurgeryXrayTableUiKey.Key, State(uid));
        }
    }

    private void OnOpen(Entity<SurgeryXrayTableComponent> ent, ref AfterActivatableUIOpenEvent args)
    {
        _ui.SetUiState(ent.Owner, SurgeryXrayTableUiKey.Key, State(ent.Owner));
    }

    private void OnStrapped(Entity<SurgeryXrayTableComponent> ent, ref StrappedEvent args)
    {
        ent.Comp.Lit = true;
        Dirty(ent);
    }

    private void OnUnstrapped(Entity<SurgeryXrayTableComponent> ent, ref UnstrappedEvent args)
    {
        ent.Comp.Lit = Patient(ent.Owner) != null;
        Dirty(ent);
    }

    private void OnStart(Entity<SurgeryXrayTableComponent> ent, ref SurgeryXrayTableStartMessage args)
    {
        var patient = Patient(ent.Owner);
        if (patient == null || args.Operation.Length == 0)
            return;

        if (Covered(patient.Value, args.Operation))
        {
            _popup.PopupEntity(Loc.GetString("surgery-clothes"), patient.Value, args.Actor);
            return;
        }

        _surgery.Queue(patient.Value, args.Actor, args.Operation);
    }

    private bool Covered(EntityUid body, string operation)
    {
        if (!_prototype.TryIndex<SurgeryOperationPrototype>(operation, out var found)
            || !Enum.TryParse<LimbZone>(found.Zone, out var zone))
            return false;

        var slots = _inventory.GetSlotEnumerator(body);
        while (slots.NextItem(out var item))
        {
            if (TryComp<LimbCoverComponent>(item, out var cover) && !cover.OnlyWhenBlocking && cover.Covers(zone))
                return true;
        }

        return false;
    }

    private void OnPrint(Entity<SurgeryXrayTableComponent> ent, ref SurgeryXrayTablePrintMessage args)
    {
        var patient = Patient(ent.Owner);
        var name = patient is { } body ? Name(body) : "";
        var card = new SurgeryXrayCard { Focus = Loc.GetString("surgery-xray-empty") };
        if (patient is { } person && TryComp<LimbBodyComponent>(person, out var limbs))
        {
            var life = TryComp<MobStateComponent>(person, out var mob)
                ? SurgeryXrayReport.Life(mob.CurrentState)
                : SurgeryXrayReport.Life(default);
            TryComp<SurgeryCavityComponent>(person, out var hold);
            var zone = args.Zone == 255 ? (LimbZone?) null : (LimbZone) args.Zone;
            var machine = TryComp<HumanoidAppearanceComponent>(person, out var look)
                && SurgerySpeciesPrototype.IsMachine(_prototype, look.Species.Id);
            card = SurgeryXrayReport.Make(limbs, hold, zone, life, CompOrNull<SurgeryInfectionComponent>(person), CompOrNull<BloodstreamComponent>(person)?.BleedAmount ?? 0f, machine);
        }

        var text = SurgeryXrayReport.Paper(name, card, args.Image.Length > 0);
        _audio.PlayPvs(PrintSound, ent.Owner);
        var paper = Spawn("PaperMedicalItem", Transform(ent.Owner).Coordinates.Offset(new Vector2(0f, -0.7f)));
        _meta.SetEntityName(paper, Loc.GetString("surgery-xray-paper", ("name", name)));
        if (TryComp<PaperComponent>(paper, out var sheet))
            _paper.SetContent((paper, sheet), text);

        if (args.Image.Length > 0 && args.Image.Length <= 400000)
        {
            var shot = EnsureComp<CameraArchivePrintComponent>(paper);
            shot.Image = args.Image;
            Dirty(paper, shot);
        }
    }

    private SurgeryXrayTableState State(EntityUid table)
    {
        var state = new SurgeryXrayTableState();
        var patient = Patient(table);
        if (patient is not { } body || !TryComp<LimbBodyComponent>(body, out _))
        {
            state.Report = Loc.GetString("surgery-xray-empty");
            return state;
        }

        state.Patient = GetNetEntity(body);
        state.Name = Name(body);
        return state;
    }

    private EntityUid? Patient(EntityUid table)
    {
        var query = EntityQueryEnumerator<BuckleComponent>();
        while (query.MoveNext(out var uid, out var buckle))
        {
            if (buckle.BuckledTo == table)
                return uid;
        }

        return null;
    }
}
