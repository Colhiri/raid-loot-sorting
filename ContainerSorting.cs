using System;
using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;

namespace RaidLootSorting;

internal static class ContainerSorting
{
    private const string AllItems = "54009119af1c881c07000029";
    private const string Meds = "543be5664bdc2dd4348b4569";
    private const string MedicalSupplies = "57864c8c245977548867e7f1";
    private const string Stimulants = "5448f3a64bdc2d60728b456a";
    private const string Morphine = "544fb3f34bdc2d03748b456a";
    private const string Grenades = "543be6564bdc2df4348b4568";
    private const string Ammo = "5485a8684bdc2da71d8b4567";
    private const string AmmoBoxes = "543be5cb4bdc2deb348b4568";
    private const string Magazines = "5448bc234bdc2d3c308b4569";
    private const string Weapons = "5422acb9af1c889c16000029";
    private const string Mods = "5448fe124bdc2da5018b4567";
    private const string Armor = "57bef4c42459772e8d35a53b";
    private const string FoodDrink = "543be6674bdc2df1348b4569";
    private const string Keys = "543be5e94bdc2df1348b4568";
    private const string Money = "543be5dd4bdc2deb348b4569";
    private const string Barter = "5448eb774bdc2d0a728b4567";
    private const string Info = "5448ecbe4bdc2d60728b4568";
    private const string Special = "5447e0e74bdc2d3c308b4567";
    private const string Flyers = "6759673c76e93d8eb20b2080";

    private const string InjectorCase = "619cbf7d23893217ec30b689";
    private const string MedCase = "5aafbcd986f7745e590fff23";
    private const string GrenadeCase = "5e2af55f86f7746d4159f07c";
    private const string AmmoCase = "5aafbde786f774389d0cbc0f";
    private const string MagazineCase = "5c127c4486f7745625356c13";
    private const string WeaponCase = "59fb023c86f7746d0d4b423c";
    private const string ThiccWeaponCase = "5b6d9ce188a4501afc1b2b25";
    private const string FoodCase = "5c093db286f7740a1b2617e3";
    private const string JunkCase = "5b7c710788a4506dec015957";
    private const string ItemsCase = "59fb042886f7746c5005a7b2";
    private const string ThiccItemsCase = "5c0a840b86f7742ffa4f2482";

    internal enum Category
    {
        Junk, Injectors, Medicine, Grenades, Ammunition, Magazines, Weapons, Attachments, Armor,
        Food, Keys, Money, Other, Storage
    }

    internal static bool IsJunk(Item item) => IsType(item, Barter) || IsType(item, Info)
        || IsType(item, Special) || IsType(item, Flyers);

    internal static bool IsJunkCase(CompoundItem item) => item.StringTemplateId == JunkCase;

    internal static Category Group(Item item)
    {
        if (InventoryTree.IsStorage(item)) return Category.Storage;
        if (IsType(item, Stimulants) || item.StringTemplateId == Morphine) return Category.Injectors;
        if (IsType(item, Meds) || IsType(item, MedicalSupplies)) return Category.Medicine;
        if (IsType(item, Grenades)) return Category.Grenades;
        if (IsType(item, Ammo) || IsType(item, AmmoBoxes)) return Category.Ammunition;
        if (IsType(item, Magazines)) return Category.Magazines;
        if (IsType(item, Weapons)) return Category.Weapons;
        if (IsType(item, Mods)) return Category.Attachments;
        if (IsType(item, Armor)) return Category.Armor;
        if (IsType(item, FoodDrink)) return Category.Food;
        if (IsType(item, Keys)) return Category.Keys;
        if (IsType(item, Money)) return Category.Money;
        return IsJunk(item) ? Category.Junk : Category.Other;
    }

    internal static IEnumerable<StashGridClass> Destinations(Item item, Category category,
        IEnumerable<CompoundItem> containers, bool junkOnly, bool improveCurrent)
    {
        var current = item.CurrentAddress?.Container?.ParentItem as CompoundItem;
        int currentRank = improveCurrent && current != null && InventoryTree.IsStorage(current)
            && current is not StashItemClass
            ? Rank(current, category) : int.MaxValue;

        return containers
            .Where(container => container != current)
            .SelectMany(container => container.Grids)
            .Where(grid => grid.CheckCompatibility(item))
            .Select(grid => (Grid: grid, Rank: junkOnly ? 0 : Rank(grid.ParentItem, category)))
            .Where(entry => entry.Rank < currentRank)
            .OrderBy(entry => entry.Rank)
            .ThenByDescending(entry => entry.Grid.Items.Count(other => other.TemplateId == item.TemplateId))
            .ThenByDescending(entry => entry.Grid.Items.Count(other => Group(other) == category))
            .Select(entry => entry.Grid);
    }

    private static int Rank(CompoundItem container, Category category)
    {
        string template = container.StringTemplateId;
        bool preferred = category switch
        {
            Category.Injectors => template == InjectorCase,
            Category.Medicine => template == MedCase,
            Category.Grenades => template == GrenadeCase,
            Category.Ammunition => template == AmmoCase,
            Category.Magazines => template == MagazineCase,
            Category.Weapons or Category.Attachments => template is WeaponCase or ThiccWeaponCase,
            Category.Food => template == FoodCase,
            Category.Junk => template == JunkCase,
            _ => false
        };
        if (preferred) return 0;
        if (category == Category.Injectors && template == MedCase) return 1;
        if (template is ItemsCase or ThiccItemsCase) return category == Category.Junk ? 1 : 3;
        if (template == JunkCase) return 4;

        // Dedicated containers for keys, money, plates and modded categories expose allow lists.
        bool specialized = container.Grids.All(grid => grid.Filters is { Length: > 0 }
            && grid.Filters.All(filter => filter.Filter is { Length: > 0 and <= 12 }
                && filter.Filter.All(allowed => allowed.ToString() != AllItems)));
        return specialized ? 2 : 5;
    }

    private static bool IsType(Item item, string template) => item.StringTemplateId == template || item.Template.IsChildOf(template);
}
