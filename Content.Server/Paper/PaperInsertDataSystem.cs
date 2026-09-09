// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.CartridgeLoader;
using Content.Server.CartridgeLoader.Cartridges;
using Content.Server.CrewManifest;
using Content.Server.Station.Systems;
using Content.Shared.Access.Systems;
using Content.Shared.CCVar;
using Content.Shared.CrewManifest;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Paper;
using Content.Shared.PDA;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;

namespace Content.Server.Paper;

public sealed class PaperInsertDataSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly CrewManifestSystem _crewManifest = default!;
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<PaperComponent>(PaperComponent.PaperUiKey.Write, subs =>
        {
            subs.Event<PaperInsertDataRequestMessage>(OnRequest);
        });
    }

    private void OnRequest(Entity<PaperComponent> ent, ref PaperInsertDataRequestMessage msg)
    {
        var actor = msg.Actor;

        var station = _station.GetOwningStation(actor);
        var stationName = station is { } stationId ? MetaData(stationId).EntityName : null;

        var characterName = MetaData(actor).EntityName;

        string? characterJob = null;
        if (_idCard.TryFindIdCard(actor, out var idCard))
            characterJob = idCard.Comp.LocalizedJobTitle;

        var manifest = new List<CrewManifestEntry>();
        var manifestStatus = PaperInsertManifestStatus.Unavailable;
        if (station is { } manifestStation && _configuration.GetCVar(CCVars.CrewManifestUnsecure))
        {
            manifestStatus = PaperInsertManifestStatus.RequiresPda;
            if (HasManifestPda(actor))
            {
                manifestStatus = PaperInsertManifestStatus.Available;
                var (_, entries) = _crewManifest.GetCrewManifest(manifestStation);
                if (entries != null)
                    manifest.AddRange(entries.Entries);
            }
        }

        var response = new PaperInsertDataResponseMessage(stationName, characterName, characterJob, manifest, manifestStatus);
        _ui.ServerSendUiMessage(ent.Owner, msg.UiKey, response, actor);
    }

    private bool HasManifestPda(EntityUid uid)
    {
        foreach (var held in _hands.EnumerateHeld(uid))
        {
            if (HasManifestProgram(held))
                return true;
        }

        var slots = _inventory.GetSlotEnumerator(uid);
        while (slots.NextItem(out var item))
        {
            if (HasManifestProgram(item))
                return true;
        }

        return false;
    }

    private bool HasManifestProgram(EntityUid uid)
    {
        return HasComp<PdaComponent>(uid) && _cartridgeLoader.HasProgram<CrewManifestCartridgeComponent>(uid);
    }
}
