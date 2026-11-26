// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Loadout;

/// <summary>
/// Which visible loadout groups of a role can change which inventory slot. Built from data only: a group changes a
/// slot when at least one of its loadouts (inline gear or its starting gear) equips something there. Loadouts that put
/// items into a container's storage map to the <see cref="StorageSlot"/> pseudo-slot.
/// </summary>
public sealed class LoadoutSlotMap
{
    /// <summary>Pseudo-slot for loadouts that only add items to storage (backpack contents and so on).</summary>
    public const string StorageSlot = "#storage";

    /// <summary>Visible groups of the role in prototype order.</summary>
    public readonly List<LoadoutGroupPrototype> Groups = new();

    /// <summary>Slots each visible group can change.</summary>
    public readonly Dictionary<string, HashSet<string>> GroupSlots = new();

    /// <summary>Visible groups that can change each slot, in role order.</summary>
    public readonly Dictionary<string, List<LoadoutGroupPrototype>> SlotGroups = new();

    public static LoadoutSlotMap Build(RoleLoadoutPrototype role, IPrototypeManager protoManager)
    {
        var map = new LoadoutSlotMap();

        foreach (var groupId in role.Groups)
        {
            if (!protoManager.Resolve(groupId, out var group) || group.Hidden)
                continue;

            map.Groups.Add(group);
            var slots = new HashSet<string>();
            map.GroupSlots[group.ID] = slots;

            foreach (var loadoutId in group.Loadouts)
            {
                if (!protoManager.Resolve(loadoutId, out var loadout))
                    continue;

                if (protoManager.Resolve(loadout.StartingGear, out var gear))
                    CollectSlots(gear, slots);

                CollectSlots(loadout, slots);
            }

            foreach (var slot in slots)
            {
                if (!map.SlotGroups.TryGetValue(slot, out var groups))
                {
                    groups = new List<LoadoutGroupPrototype>();
                    map.SlotGroups[slot] = groups;
                }

                groups.Add(group);
            }
        }

        return map;
    }

    /// <summary>Groups that can change <paramref name="slot"/>; empty when nothing can.</summary>
    public IReadOnlyList<LoadoutGroupPrototype> GetGroups(string slot)
    {
        return SlotGroups.TryGetValue(slot, out var groups) ? groups : Array.Empty<LoadoutGroupPrototype>();
    }

    /// <summary>True when the group changes at least one of the given shown slots.</summary>
    public bool MapsToAny(LoadoutGroupPrototype group, IReadOnlyCollection<string> shownSlots)
    {
        if (!GroupSlots.TryGetValue(group.ID, out var slots))
            return false;

        foreach (var slot in slots)
        {
            if (shownSlots.Contains(slot))
                return true;
        }

        return false;
    }

    private static void CollectSlots(IEquipmentLoadout gear, HashSet<string> slots)
    {
        foreach (var (slot, item) in gear.Equipment)
        {
            if (!string.IsNullOrEmpty(item.Id))
                slots.Add(slot);
        }

        foreach (var items in gear.Storage.Values)
        {
            if (items is { Count: > 0 })
            {
                slots.Add(StorageSlot);
                break;
            }
        }
    }
}
