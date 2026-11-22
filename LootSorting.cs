using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Diz.LanguageExtensions;
using EFT.Communications;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using UIFixes;
using static RaidLootSorting.InventoryTree;

namespace RaidLootSorting;

internal static class LootSorting
{
    private static readonly AccessTools.FieldRef<ItemUiContext, InventoryController> ContextController =
        AccessTools.FieldRefAccess<ItemUiContext, InventoryController>("_inventoryController");

    private static bool busy;

    private enum Outcome { Moved, Unavailable, NoSpace, Interrupted }

    internal static bool TryGetStash(ItemUiContext? context, out BackEndInventoryController player, out Stash stash)
    {
        if (context != null && context.ContextType == EItemUiContextType.InventoryScreen && !RaidLootPlugin.InRaid()
            && ContextController(context) is BackEndInventoryController current && current.Inventory?.Stash is { } currentStash)
        {
            player = current;
            stash = currentStash;
            return true;
        }

        player = null!;
        stash = null!;
        return false;
    }

    internal static void Unload(string profileId)
    {
        if (TryGetStash(ItemUiContext.Instance, out var player, out var stash) && player._clientProfile.Id == profileId)
        {
            _ = Run(() => UnloadEquipment(player, stash));
        }
    }

    internal static void Distribute(BackEndInventoryController player, Stash stash, Item[] selection)
        => _ = Run(() => DistributeSelection(player, stash, selection));

    private static async Task Run(Func<Task> action)
    {
        if (busy)
        {
            return;
        }

        busy = true;
        try
        {
            await action();
        }
        catch (Exception error)
        {
            RaidLootPlugin.Log.LogError(error);
            NotificationManager.DisplayWarningNotification("Сортировка остановлена из-за ошибки переноса.");
        }
        finally
        {
            busy = false;
        }
    }

    private static async Task UnloadEquipment(BackEndInventoryController player, Stash stash)
    {
        var equipment = player.Inventory.Equipment;
        var selected = MultiSelect.Enabled
            ? MultiSelect.ItemContexts.Concat(MultiSelect.SecondaryContexts)
                .Select(context => context.Item).Where(item => item.GetAllParentItems().Contains(equipment)).ToHashSet()
            : new HashSet<Item>();
        var loadout = selected.Count == 0 ? RaidLoadout.Read(player._clientProfile) : null;
        var loot = Sort(EquipmentLoot(equipment, selected, loadout)).ToArray();
        if (loot.Length == 0)
        {
            NotificationManager.DisplayMessageNotification("Нет предметов для выгрузки.");
            return;
        }

        var junkCases = StashContainers(stash).Where(ContainerSorting.IsJunkCase).ToArray();
        var overflow = new List<LootEntry>();
        var otherLoot = new List<LootEntry>();
        int moved = 0;
        foreach (var entry in loot)
        {
            if (!entry.Item.MarkedAsSpawnedInSession || !ContainerSorting.IsJunk(entry.Item))
            {
                otherLoot.Add(entry);
                continue;
            }

            if (!CanContinue(player, stash))
            {
                Report(moved, loot.Length, Outcome.Interrupted);
                return;
            }
            if (!entry.Item.GetAllParentItems().Contains(equipment) || !CanMove(entry, loadout))
            {
                continue;
            }

            var outcome = await TryContainers(player, stash, entry, junkCases, junkOnly: true);
            if (outcome == Outcome.Interrupted)
            {
                Report(moved, loot.Length, outcome);
                return;
            }
            if (outcome == Outcome.Moved)
            {
                moved++;
            }
            else
            {
                overflow.Add(entry);
            }
        }

        var grid = stash.Grid;
        int firstRow = GridPlacement.BelowMainStash(grid);
        foreach (var group in new[] { overflow, otherLoot })
        {
            int endRow = firstRow;
            foreach (var entry in group)
            {
                if (!CanContinue(player, stash))
                {
                    Report(moved, loot.Length, Outcome.Interrupted);
                    return;
                }
                if (!entry.Item.GetAllParentItems().Contains(equipment) || !CanMove(entry, loadout))
                {
                    continue;
                }

                var (outcome, itemEndRow) = await TryGrid(player, entry, grid, firstRow);
                if (outcome is Outcome.NoSpace or Outcome.Interrupted)
                {
                    Report(moved, loot.Length, outcome);
                    return;
                }
                if (outcome == Outcome.Moved)
                {
                    moved++;
                    endRow = Math.Max(endRow, itemEndRow);
                }
            }

            if (group == overflow && endRow > firstRow)
            {
                firstRow = GridPlacement.AfterDivider(grid, endRow);
            }
        }

        Report(moved, loot.Length, Outcome.Unavailable);
    }

    private static async Task DistributeSelection(BackEndInventoryController player, Stash stash, Item[] selection)
    {
        if (!CanContinue(player, stash))
        {
            return;
        }

        var roots = SelectionRoots(selection.Where(item => InStash(item, stash)).ToHashSet()).ToArray();
        var sources = roots.OfType<CompoundItem>().Where(item => IsStorage(item) && !item.IsEmpty).ToHashSet();
        var loot = Sort(roots.SelectMany(item => item is CompoundItem source && sources.Contains(source)
            ? source.Grids.SelectMany(grid => grid.Items)
            : new[] { item }).Select(item => new LootEntry(item))).ToArray();
        var excluded = roots.SelectMany(item => item.GetAllItems()).ToHashSet();
        var containers = StashContainers(stash).Where(container => !excluded.Contains(container)).ToArray();

        int moved = 0;
        foreach (var entry in loot)
        {
            if (!CanContinue(player, stash))
            {
                Report(moved, loot.Length, Outcome.Interrupted);
                return;
            }
            if (!InStash(entry.Item, stash) || !CanMove(entry))
            {
                continue;
            }

            var parent = entry.Item.CurrentAddress.Container.ParentItem as CompoundItem;
            var outcome = await TryContainers(player, stash, entry, containers, improveCurrent: parent == null || !sources.Contains(parent));
            if (outcome == Outcome.Unavailable && entry.Item.CurrentAddress.Container != stash.Grid)
            {
                (outcome, _) = await TryGrid(player, entry, stash.Grid);
            }
            if (outcome == Outcome.Interrupted)
            {
                Report(moved, loot.Length, outcome);
                return;
            }
            if (outcome == Outcome.Moved)
            {
                moved++;
            }
        }

        Report(moved, loot.Length, Outcome.Unavailable);
    }

    private static bool CanMove(LootEntry entry, IReadOnlyDictionary<string, int>? loadout = null)
        => CanTake(entry.Item) && entry.Item.StackObjectsCount > entry.KeepCount
            && !HasProtectedDescendants(entry.Item, loadout)
            && (!entry.EmptyStorageFirst || ((CompoundItem)entry.Item).IsEmpty);

    private static bool CanContinue(BackEndInventoryController player, Stash stash)
        => TryGetStash(ItemUiContext.Instance, out var currentPlayer, out var currentStash)
            && currentPlayer == player && currentStash == stash;

    private static IEnumerable<CompoundItem> StashContainers(Stash stash)
        => stash.GetAllItems().OfType<CompoundItem>()
            .Where(item => item != stash && IsStorage(item) && item.CurrentAddress is GridItemAddress && InStash(item, stash));

    private static IOrderedEnumerable<LootEntry> Sort(IEnumerable<LootEntry> entries) => entries
        .OrderBy(entry => ContainerSorting.Group(entry.Item))
        .ThenByDescending(entry => entry.EmptyStorageFirst ? entry.Item.GetAllParentItems().Count() : 0)
        .ThenBy(entry => entry.Item.StringTemplateId, StringComparer.Ordinal);

    private static async Task<Outcome> TryContainers(BackEndInventoryController player, Stash stash, LootEntry entry,
        IEnumerable<CompoundItem> containers, bool junkOnly = false, bool improveCurrent = false)
    {
        var item = entry.Item;
        var available = containers.Where(container => InStash(container, stash));
        foreach (var grid in ContainerSorting.Destinations(item, ContainerSorting.Group(item), available, junkOnly, improveCurrent))
        {
            var (outcome, _) = await TryGrid(player, entry, grid);
            if (outcome is Outcome.Moved or Outcome.Interrupted)
            {
                return outcome;
            }
        }

        return Outcome.Unavailable;
    }

    private static async Task<(Outcome Outcome, int EndRow)> TryGrid(BackEndInventoryController player, LootEntry entry,
        Grid grid, int firstRow = 0)
    {
        if (!grid.CheckCompatibility(entry.Item))
        {
            return (Outcome.Unavailable, firstRow);
        }

        var result = Outcome.NoSpace;
        foreach (var location in GridPlacement.FreeLocations(grid, entry.Item, firstRow))
        {
            int endRow = location.y + entry.Item.CalculateRotatedSize(location.r).Y;
            result = await Move(player, entry, grid.CreateItemAddress(location));
            if (result != Outcome.Unavailable)
            {
                return (result, endRow);
            }
        }

        return (result, firstRow);
    }

    private static async Task<Outcome> Move(BackEndInventoryController player, LootEntry entry, ItemAddress address)
    {
        var item = entry.Item;
        OperationResult operation = entry.KeepCount > 0
            ? ItemManipulator.SplitExact(item, item.StackObjectsCount - entry.KeepCount, address, player, player, simulate: true)
            : ItemManipulator.Move(item, address, player, simulate: true);

        if (operation.Failed || !player.CanExecute(operation.Value)
            || operation.Value is IDestroyResult { ItemsDestroyRequired: true })
        {
            return Outcome.Unavailable;
        }

        var result = await player.TryRunNetworkTransaction(operation);
        if (result.Succeed)
        {
            // The local callback fires before the game adds the command to its outgoing queue.
            await Task.Yield();
            result = await player._backendSession.FlushOperationQueue();
            if (result.Succeed)
            {
                return Outcome.Moved;
            }
        }

        RaidLootPlugin.Log.LogWarning(result.Error);
        return Outcome.Interrupted;
    }

    private static void Report(int moved, int total, Outcome outcome)
    {
        if (outcome == Outcome.Interrupted)
        {
            NotificationManager.DisplayWarningNotification($"Перенос прерван. Перемещено: {moved}.");
        }
        else if (outcome == Outcome.NoSpace)
        {
            NotificationManager.DisplayWarningNotification($"В зоне выгрузки схрона нет места для следующего предмета. Перемещено: {moved}, осталось: {total - moved}.");
        }
        else if (moved == 0)
        {
            NotificationManager.DisplayMessageNotification("Нет предметов, которые можно переместить в подходящее место.");
        }
        else
        {
            NotificationManager.DisplayMessageNotification($"Перемещено: {moved}, осталось на месте: {total - moved}.");
        }
    }
}
