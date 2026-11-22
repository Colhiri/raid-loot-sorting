using EFT;

namespace RaidLootSorting;

internal static class ModText
{
    internal static string Translate(string english, string russian)
        => LocalizationManager.Instance?.Culture == LocalizationManager.RUSSIAN_LOCALIZATION ? russian : english;
}
