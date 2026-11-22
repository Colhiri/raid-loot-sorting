using EFT;

namespace RaidLootSorting;

internal static class ModText
{
    internal static string Translate(string english, string russian)
        => LocaleManagerClass.LocaleManagerClass?.String_0 == LocaleManagerClass.RUSSIAN_LOCALIZATION ? russian : english;
}
