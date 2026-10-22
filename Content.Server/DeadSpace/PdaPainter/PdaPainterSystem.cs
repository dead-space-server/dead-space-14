using Content.Server.UserInterface;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeadSpace.PdaPainter;
using Content.Shared.PDA;
using Content.Shared.UserInterface;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.PdaPainter;

/// <summary>
/// Handles the PDA painter machine: keeps the server canvas, applies
/// free-hand paintings and templates to the inserted PDA on save.
/// Templates are all entity prototypes with a PdaComponent; the applied
/// sprite state comes from the prototype's appearance data init.
/// </summary>
public sealed class PdaPainterSystem : EntitySystem
{
    public const int CanvasSize = 32;

    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    private List<PdaPainterTemplateInfo>? _templates;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PdaPainterComponent, PdaPainterDrawMessage>(OnDraw);
        SubscribeLocalEvent<PdaPainterComponent, PdaPainterSaveMessage>(OnSave);
        SubscribeLocalEvent<PdaPainterComponent, PdaPainterResetMessage>(OnReset);
        SubscribeLocalEvent<PdaPainterComponent, AfterActivatableUIOpenEvent>(OnUiOpen);

        SubscribeLocalEvent<PdaPainterComponent, EntRemovedFromContainerMessage>(OnItemRemoved);
    }

    private void OnUiOpen(Entity<PdaPainterComponent> ent, ref AfterActivatableUIOpenEvent args)
    {
        PushState(ent);
    }

    private void OnItemRemoved(Entity<PdaPainterComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != PdaPainterComponent.SlotId)
            return;

        PushState(ent);
    }

    private void OnDraw(Entity<PdaPainterComponent> ent, ref PdaPainterDrawMessage args)
    {
        var canvas = ent.Comp.Canvas;
        for (var i = 0; i < args.Indices.Count && i < args.Colors.Count; i++)
        {
            var index = args.Indices[i];
            if (index < 0 || index >= CanvasSize * CanvasSize)
                continue;

            var color = args.Colors[i];
            if (color == 0)
                canvas.Remove(index);
            else
                canvas[index] = color;
        }
    }

    private void OnSave(Entity<PdaPainterComponent> ent, ref PdaPainterSaveMessage args)
    {
        var (uid, comp) = ent;
        if (!_itemSlots.TryGetSlot(uid, PdaPainterComponent.SlotId, out var slot))
            return;

        if (slot.Item is not { } pda)
            return;

        if (!TryComp<PdaComponent>(pda, out _))
            return;

        if (comp.Canvas.Count == 0)
        {
            // Saving an empty canvas wipes the painting entirely.
            RemComp<PdaPaintedComponent>(pda);
        }
        else
        {
            var painted = EnsureComp<PdaPaintedComponent>(pda);
            painted.Pixels = new Dictionary<int, int>(comp.Canvas);
            Dirty(pda, painted);
        }

        // Apply the selected template by switching the PDA's base sprite state.
        comp.SelectedTemplate = args.TemplateId;
        if (args.TemplateId != null)
        {
            var state = GetTemplateState(args.TemplateId);
            if (state != null)
                _appearance.SetData(pda, PdaVisuals.PdaType, state);
        }

        _audio.PlayPvs("/Audio/Effects/spray.ogg", uid);
        PushState(ent);
    }

    private void OnReset(Entity<PdaPainterComponent> ent, ref PdaPainterResetMessage args)
    {
        var (uid, comp) = ent;
        comp.Canvas.Clear();
        comp.SelectedTemplate = null;

        // Wipe the painting off the inserted PDA and restore its original
        // base sprite state from the prototype.
        if (_itemSlots.TryGetSlot(uid, PdaPainterComponent.SlotId, out var slot) &&
            slot.Item is { } pda &&
            TryComp<PdaComponent>(pda, out _))
        {
            RemComp<PdaPaintedComponent>(pda);

            var protoId = MetaData(pda).EntityPrototype?.ID;
            var state = protoId != null ? GetTemplateState(protoId) : null;
            _appearance.SetData(pda, PdaVisuals.PdaType, state ?? "pda");
        }

        PushState(ent);
    }

    private void PushState(Entity<PdaPainterComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, PdaPainterUiKey.Key))
            return;

        _ui.SetUiState(ent.Owner, PdaPainterUiKey.Key, BuildState(ent));
    }

    private PdaPainterBoundUserInterfaceState BuildState(Entity<PdaPainterComponent> ent)
    {
        var comp = ent.Comp;
        _templates ??= BuildTemplateList();

        string? previewProtoId = null;
        string? previewName = null;

        if (_itemSlots.TryGetSlot(ent.Owner, PdaPainterComponent.SlotId, out var slot) && slot.Item is { } pda)
        {
            previewName = Name(pda);
            previewProtoId = MetaData(pda).EntityPrototype?.ID;
        }

        return new PdaPainterBoundUserInterfaceState(
            new Dictionary<int, int>(comp.Canvas),
            _templates,
            comp.SelectedTemplate,
            previewProtoId,
            previewName);
    }

    private List<PdaPainterTemplateInfo> BuildTemplateList()
    {
        var list = new List<PdaPainterTemplateInfo>();
        var pdaName = _factory.GetComponentName(typeof(PdaComponent));

        foreach (var proto in _proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract)
                continue;

            if (!proto.Components.ContainsKey(pdaName))
                continue;

            list.Add(new PdaPainterTemplateInfo(proto.ID, proto.Name));
        }

        list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
        return list;
    }

    /// <summary>
    /// Returns the base sprite state of a PDA prototype (from its appearance
    /// data init), or null if the prototype is not a valid template.
    /// </summary>
    private string? GetTemplateState(string protoId)
    {
        if (!_proto.TryIndex<EntityPrototype>(protoId, out var proto))
            return null;

        var pdaName = _factory.GetComponentName(typeof(PdaComponent));
        if (!proto.Components.ContainsKey(pdaName))
            return null;

        // Read the state from a temporary server-side instance of the prototype.
        var ent = Spawn(protoId, MapCoordinates.Nullspace);
        string? state = null;
        if (_appearance.TryGetData<string>(ent, PdaVisuals.PdaType, out var data))
            state = data;
        Del(ent);
        return state;
    }
}
