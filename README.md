# Raid Loot Sorting

[Русский](README.ru.md)

Unload raid loot from your PMC and sort selected stash items into containers. Raid Loot Sorting is a client-side SPT mod. The tooltip, context menu and notifications follow the game language. The mod uses Russian in the Russian client and English in all other languages.

## Compatibility

| SPT | Mod version | UI Fixes | Source branch |
| --- | --- | --- | --- |
| 4.1.6 | 1.2.2 | [6.0.4](https://github.com/tyfon7/UIFixes/releases/tag/v6.0.4) | `main` |
| 4.0.13 | 1.0.4 | [5.3.9](https://github.com/tyfon7/UIFixes/releases/tag/v5.3.9) | `support/4.0` |

Close the game, install UI Fixes for your SPT version, and extract the matching [release ZIP](https://github.com/Colhiri/raid-loot-sorting/releases) alongside `EscapeFromTarkov.exe`. The plugin is installed at `BepInEx/plugins/RaidLootSorting/RaidLootSorting.dll`.

## Four ways to use it

| Situation | What happens |
| --- | --- |
| **PMC, raid snapshot available** | Open **Character → Gear** and click the arrow above Pin and Lock beside the stash. The gear from the snapshot stays on your PMC. The mod unloads acquired items, excess units in existing stacks and newly attached removable parts. Picked-up weapons move with their attachments and ammunition. |
| **PMC, no raid snapshot** | The same button unloads all removable gear within the available space. It empties removable backpacks and rigs before moving them. This can happen on first use or after a raid started before installation. |
| **Items selected on the PMC** | Select items with UI Fixes and click the unload arrow. Your selection takes priority over the snapshot. A selected backpack, rig or other container stays equipped while its grid contents are unloaded. Other selected items are transferred as units when possible. |
| **Items selected in the stash** | Right-click a selected item in the stash or in an open stash container and choose **Distribute to containers**. It also works on a single item. A selected filled container is treated as a source for its grid contents. An empty container is treated as an item. |

The mod records item IDs and stack counts on entry into a local PMC raid and keeps the latest snapshot for each profile. The snapshot comes from the gear you actually take into the raid. Unloading starts when you click the button in the stash. Pinned, locked, quest and non-removable items stay in place. Contents that can be moved may still be unloaded from a protected container.

With the default UI Fixes bindings, use Shift + left-click to select individual items or drag a selection box from an empty grid cell.

## Where PMC loot goes

1. The mod sorts the items to unload by category and item type. Eligible barter items and other Lucky Scav Junk Box items go into compatible junk boxes only when marked **Found in Raid**. For each item it checks the available boxes and both orientations where applicable.
2. Remaining Found in Raid junk goes into the open area below the main stash after one empty row. The rest of the loot goes below that group after another empty row. If the junk boxes take all eligible junk, the rest of the loot begins after a single empty row. An empty stash starts at the first row.
3. Items already in lower rows still occupy their cells. The mod can use the open space above them. If the next item cannot fit in the unload area, the operation stops and the remaining items stay on the PMC.

## How stash distribution picks a container

The mod checks each container's in-game filters and actual free cells. It tries category-specific cases first. Containers of equal priority with more copies of the same item are preferred, followed by those with more items of the same category.

| Item | Preferred destinations |
| --- | --- |
| Injectors | Injector case, then medicine case |
| Medicine | Medicine case |
| Grenades | Grenade case |
| Ammunition and magazines | Ammo case or magazine case, according to the item type |
| Weapons and attachments | Weapon cases |
| Food and drink | Food container |
| Junk | Lucky Scav Junk Box, then compatible item cases |
| Keys, currency and other items | Compatible dedicated containers or general storage |

If a case is full or the item does not fit, the mod checks the next compatible container. If none accepts an item from inside a container, it tries free cells in the stash itself. Items already on the stash grid stay in place when there is no suitable container. If there is no free destination, the item stays where it is.

The plugin stores raid snapshots locally and uses the game's existing inventory commands to send item transfers to your SPT server. It collects no telemetry and contacts no external services.

## Uninstall and build

With the game closed, remove `BepInEx/plugins/RaidLootSorting`. To remove saved raid snapshots as well, remove `BepInEx/config/RaidLootSorting`.

Build from the branch for your SPT version using the patched client DLLs from that installation.

```powershell
dotnet build RaidLootSorting.csproj -c Release -p:SptRoot="D:/SPT"
```
