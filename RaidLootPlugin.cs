using System;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using UIFixes;

namespace RaidLootSorting;

[BepInPlugin("com.colhirux.raidlootsorting", "Raid Loot Sorting", "1.0.3")]
[BepInProcess("EscapeFromTarkov.exe")]
[BepInDependency("com.SPT.core")]
[BepInDependency("Tyfon.UIFixes", "5.3.9")]
public sealed class RaidLootPlugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;

    private void Awake()
    {
        Log = Logger;
        var harmony = new Harmony(Info.Metadata.GUID);
        harmony.CreateClassProcessor(typeof(RaidStartPatch)).Patch();
        harmony.CreateClassProcessor(typeof(UnloadButtonPatch)).Patch();
        harmony.CreateClassProcessor(typeof(StashContextMenu)).Patch();
    }

    internal static bool InRaid() => Singleton<AbstractGame>.Instance?.InRaid == true;
}

[HarmonyPatch(typeof(LocalGame), nameof(LocalGame.smethod_6))]
internal static class RaidStartPatch
{
    private static void Postfix(Profile profile, LocalRaidSettings raidSettings)
    {
        if (raidSettings.mode == ELocalMode.PVE_OFFLINE && raidSettings.playerSide == ESideType.Pmc
            && !(raidSettings.transition?.transitionCount > 0))
        {
            RaidLoadout.Capture(profile);
        }
    }
}

[HarmonyPatch(typeof(ItemUiContext), nameof(ItemUiContext.GetItemContextInteractions))]
internal static class StashContextMenu
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(Action? closeAction, out bool __state) => __state = closeAction == null;

    private static void Postfix(ItemUiContext __instance, ItemContextAbstractClass itemContext, bool __state,
        InventoryController ___inventoryController_0, ItemInfoInteractionsAbstractClass<EItemInfoButton> __result)
    {
        if (!__state || __result == null || !LootSorting.TryGetStash(__instance, out var player, out var stash)
            || ___inventoryController_0 != player || !InventoryTree.InStash(itemContext.Item, stash))
        {
            return;
        }

        var clicked = itemContext.Item;
        var selected = MultiSelect.Enabled && MultiSelect.ItemContexts.Any(context => context.Item == clicked)
            ? MultiSelect.ItemContexts.Select(context => context.Item).Where(item => InventoryTree.InStash(item, stash))
                .Distinct().ToArray()
            : new[] { clicked };
        __result.method_2("RaidLootSorting.Distribute", "Распределить по контейнерам",
            () => LootSorting.Distribute(player, stash, selected));
    }
}
