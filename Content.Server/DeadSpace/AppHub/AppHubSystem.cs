using System.Linq;
using Content.Server.CartridgeLoader;
using Content.Server.UserInterface;
using Content.Shared.CartridgeLoader;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeadSpace.AppHub;
using Content.Shared.Interaction;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.AppHub;

public sealed partial class AppHubSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private CartridgeLoaderSystem _cartridgeLoader = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AppHubComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<AppHubComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<AppHubComponent, AfterActivatableUIOpenEvent>(OnUiOpen);
        SubscribeLocalEvent<AppHubComponent, AppHubInstallMessage>(OnInstall);
        SubscribeLocalEvent<AppHubComponent, AppHubUninstallMessage>(OnUninstall);
        SubscribeLocalEvent<AppHubComponent, AppHubSelectCategoryMessage>(OnSelectCategory);
        SubscribeLocalEvent<AppHubComponent, AppHubEjectPdaMessage>(OnEjectPda);
        SubscribeLocalEvent<AppHubComponent, EntInsertedIntoContainerMessage>(OnContainerInserted);
        SubscribeLocalEvent<AppHubComponent, EntRemovedFromContainerMessage>(OnContainerRemoved);
    }

    private void OnInit(EntityUid uid, AppHubComponent comp, ComponentInit args)
    {
        _itemSlots.AddItemSlot(uid, AppHubComponent.PdaSlotId, comp.PdaSlot);
    }

    private void OnRemove(EntityUid uid, AppHubComponent comp, ComponentRemove args)
    {
        _itemSlots.RemoveItemSlot(uid, comp.PdaSlot);
    }

    private void OnContainerInserted(EntityUid uid, AppHubComponent comp, EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != comp.PdaSlot.ID)
            return;

        UpdateUiState(uid, comp);
    }

    private void OnContainerRemoved(EntityUid uid, AppHubComponent comp, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != comp.PdaSlot.ID)
            return;

        UpdateUiState(uid, comp);
    }

    private void OnUiOpen(EntityUid uid, AppHubComponent comp, AfterActivatableUIOpenEvent args)
    {
        UpdateUiState(uid, comp);
    }

    private void OnInstall(EntityUid uid, AppHubComponent comp, AppHubInstallMessage msg)
    {
        if (comp.PdaSlot.Item is not { } pda)
            return;

        if (!_prototype.TryIndex<AppCatalogEntryPrototype>(msg.ProgramId, out var entry))
            return;

        if (string.IsNullOrEmpty(entry.ProgramId))
            return;

        if (!TryComp(pda, out CartridgeLoaderComponent? loader))
            return;

        foreach (var prog in _cartridgeLoader.GetInstalled(pda))
        {
            if (Prototype(prog)?.ID == entry.ProgramId)
                return;
        }

        _cartridgeLoader.InstallProgram(pda, entry.ProgramId, loader: loader);
        UpdateUiState(uid, comp);
    }

    private void OnUninstall(EntityUid uid, AppHubComponent comp, AppHubUninstallMessage msg)
    {
        if (comp.PdaSlot.Item is not { } pda)
            return;

        if (!_prototype.TryIndex<AppCatalogEntryPrototype>(msg.ProgramId, out var entry))
            return;

        if (string.IsNullOrEmpty(entry.ProgramId))
            return;

        if (!TryComp(pda, out CartridgeLoaderComponent? loader))
            return;

        foreach (var prog in _cartridgeLoader.GetInstalled(pda))
        {
            if (Prototype(prog)?.ID == entry.ProgramId)
            {
                _cartridgeLoader.UninstallProgram(pda, prog, loader);
                break;
            }
        }

        UpdateUiState(uid, comp);
    }

    private void OnSelectCategory(EntityUid uid, AppHubComponent comp, AppHubSelectCategoryMessage msg)
    {
        comp.SelectedCategory = msg.Category;
        UpdateUiState(uid, comp);
    }

    private void OnEjectPda(EntityUid uid, AppHubComponent comp, AppHubEjectPdaMessage msg)
    {
        if (comp.PdaSlot.Item is not { } pda)
            return;

        _itemSlots.TryEject(uid, comp.PdaSlot, msg.Actor, out _);
        UpdateUiState(uid, comp);
    }

    private void UpdateUiState(EntityUid uid, AppHubComponent comp)
    {
        if (!_ui.HasUi(uid, AppHubUiKey.Key))
            return;

        var usedDisk = 0;
        var maxDisk = 0;
        var installedProgramIds = new HashSet<string>();

        if (comp.PdaSlot.Item is { } pda && TryComp(pda, out CartridgeLoaderComponent? loader))
        {
            maxDisk = loader.DiskSpace;
            usedDisk = _cartridgeLoader.GetInstalled(pda).Count;

            foreach (var prog in _cartridgeLoader.GetInstalled(pda))
            {
                var protoId = Prototype(prog)?.ID;
                if (protoId != null)
                    installedProgramIds.Add(protoId);
            }
        }

        var catalogEntries = new List<AppHubCatalogEntry>();
        foreach (var proto in _prototype.EnumeratePrototypes<AppCatalogEntryPrototype>())
        {
            if (comp.SelectedCategory != "All" && proto.Category != comp.SelectedCategory)
                continue;

            catalogEntries.Add(new AppHubCatalogEntry
            {
                Id = proto.ID,
                Name = Loc.GetString(proto.Name),
                Description = Loc.GetString(proto.Description),
                Category = proto.Category,
                IsInstalled = installedProgramIds.Contains(proto.ProgramId)
            });
        }

        var state = new AppHubUiState(comp.PdaSlot.Item != null, usedDisk, maxDisk, comp.SelectedCategory, catalogEntries);
        _ui.SetUiState(uid, AppHubUiKey.Key, state);
    }
}
