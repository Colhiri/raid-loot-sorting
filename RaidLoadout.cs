using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EFT;
using EFT.InventoryLogic;
using Newtonsoft.Json;

namespace RaidLootSorting;

internal static class RaidLoadout
{
    private static string? cachedProfile;
    private static Dictionary<string, int>? cachedLoadout;

    internal static void Capture(Profile profile)
    {
        cachedProfile = profile.Id;
        cachedLoadout = null;
        string path = SnapshotPath(profile.Id);
        try
        {
            var snapshot = profile.Inventory.Equipment.GetAllItems().ToDictionary(item => item.Id, item => item.StackObjectsCount);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string stagingPath = path + ".tmp";
            File.WriteAllText(stagingPath, JsonConvert.SerializeObject(snapshot));
            if (File.Exists(path))
            {
                File.Replace(stagingPath, path, null);
            }
            else
            {
                File.Move(stagingPath, path);
            }

            cachedLoadout = snapshot;
        }
        catch (Exception error)
        {
            RaidLootPlugin.Log.LogError(error);
            try
            {
                File.Delete(path);
            }
            catch (Exception deleteError)
            {
                RaidLootPlugin.Log.LogError(deleteError);
            }
        }
    }

    internal static IReadOnlyDictionary<string, int>? Read(Profile profile)
    {
        if (profile.Id == cachedProfile)
        {
            return cachedLoadout?.ContainsKey(profile.Inventory.Equipment.Id) == true ? cachedLoadout : null;
        }

        try
        {
            string path = SnapshotPath(profile.Id);
            if (File.Exists(path))
            {
                var snapshot = JsonConvert.DeserializeObject<Dictionary<string, int>>(File.ReadAllText(path));
                return snapshot?.ContainsKey(profile.Inventory.Equipment.Id) == true ? snapshot : null;
            }
        }
        catch (Exception error)
        {
            RaidLootPlugin.Log.LogError(error);
        }

        return null;
    }

    private static string SnapshotPath(string profileId) => Path.Combine(BepInEx.Paths.ConfigPath, "RaidLootSorting", profileId + ".json");
}

internal static class InventoryTree
{
    internal static bool InStash(Item item, StashItemClass stash)
        => item.GetAllParentLocations().Any(address => address.Container == stash.Grid);

    internal static bool CanTake(Item item)
        => !item.QuestItem && !IsProtected(item) && item.CurrentAddress is { } address
            && (address.Container is not Slot slot || (!slot.Locked && !item.IsUnremovable))
            && !item.GetAllParentItems().Any(parent => parent is AmmoBox or RandomLootContainerItemClass);

    internal static bool IsProtected(Item item) => item.PinLockState is EItemPinLockState.Pinned or EItemPinLockState.Locked;

    internal static IEnumerable<Item> SelectionRoots(HashSet<Item> selection)
        => selection.Where(item => !item.GetAllParentItems().Any(selection.Contains));

    internal static IEnumerable<LootEntry> EquipmentLoot(Item equipment, HashSet<Item> selection,
        IReadOnlyDictionary<string, int>? loadout)
        => selection.Count == 0
            ? RaidLoot(equipment, loadout)
            : SelectionRoots(selection).SelectMany(item => IsStorage(item)
                ? RaidLoot(item, loadout: null, gridsOnly: true)
                : new[] { new LootEntry(item) });

    internal static IEnumerable<LootEntry> RaidLoot(Item parent, IReadOnlyDictionary<string, int>? loadout, bool gridsOnly = false)
    {
        if (parent is not GClass3248 collection || parent is AmmoBox or RandomLootContainerItemClass)
        {
            yield break;
        }

        foreach (var child in collection.Containers.Where(container => !gridsOnly || container is StashGridClass)
                     .SelectMany(container => container.Items))
        {
            var equipment = parent as InventoryEquipment;
            var slot = child.CurrentAddress?.Container;
            if (equipment != null && slot == equipment.GetSlot(EquipmentSlot.Dogtag))
            {
                continue;
            }

            int originalCount = 0;
            bool retained = loadout != null && loadout.TryGetValue(child.Id, out originalCount);
            bool isPockets = equipment != null && slot == equipment.GetSlot(EquipmentSlot.Pockets);
            if (retained || isPockets || !CanTake(child))
            {
                if (retained && child.StackObjectsCount > originalCount && CanTake(child))
                {
                    yield return new LootEntry(child, originalCount);
                }
                if (!retained && child is Weapon)
                {
                    continue;
                }
                foreach (var entry in RaidLoot(child, loadout, gridsOnly: !retained && IsStorage(child)))
                {
                    yield return entry;
                }
                continue;
            }

            // Detachable parts and ammunition stay with newly picked-up weapons.
            if (IsStorage(child))
            {
                foreach (var entry in RaidLoot(child, loadout, gridsOnly: true))
                {
                    yield return entry;
                }
                yield return new LootEntry(child, emptyStorageFirst: true);
            }
            else if (!HasProtectedDescendants(child, loadout))
            {
                yield return new LootEntry(child);
            }
            else if (child is not Weapon)
            {
                foreach (var entry in RaidLoot(child, loadout))
                {
                    yield return entry;
                }
            }
        }
    }

    internal static bool HasProtectedDescendants(Item item, IReadOnlyDictionary<string, int>? loadout = null)
        => item.GetAllItems().Any(child => child != item
            && (child.QuestItem || IsProtected(child) || loadout?.ContainsKey(child.Id) == true));

    internal static bool IsStorage(Item item) => item is CompoundItem { Grids.Length: > 0 }
        && item is not AmmoBox and not RandomLootContainerItemClass and not Weapon;

    internal readonly struct LootEntry
    {
        internal Item Item { get; }
        internal int KeepCount { get; }
        internal bool EmptyStorageFirst { get; }

        internal LootEntry(Item item, int keepCount = 0, bool emptyStorageFirst = false)
        {
            Item = item;
            KeepCount = keepCount;
            EmptyStorageFirst = emptyStorageFirst;
        }
    }
}
