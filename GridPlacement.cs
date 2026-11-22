using System;
using System.Collections.Generic;
using EFT.InventoryLogic;

namespace RaidLootSorting;

internal static class GridPlacement
{
    internal static int BelowMainStash(StashGridClass grid)
    {
        var occupied = OccupiedRows(grid);
        int firstOccupied = Array.IndexOf(occupied, true);
        if (firstOccupied < 0)
        {
            return 0;
        }

        // Leave a divider above the widest open band below the occupied part of the stash.
        // Individual items parked farther down remain obstacles, not part of the upper block.
        int bandStart = firstOccupied + 1;
        int longest = 0;
        int firstRow = occupied.Length;
        for (int row = bandStart; row < occupied.Length; row++)
        {
            if (occupied[row])
            {
                bandStart = row + 1;
                continue;
            }

            int length = row - bandStart + 1;
            if (length >= longest)
            {
                longest = length;
                firstRow = bandStart + 1;
            }
        }

        return firstRow;
    }

    internal static int AfterDivider(StashGridClass grid, int firstRow)
    {
        var occupied = OccupiedRows(grid);
        while (firstRow < occupied.Length && occupied[firstRow])
        {
            firstRow++;
        }

        return Math.Min(firstRow + 1, occupied.Length);
    }

    internal static IEnumerable<LocationInGrid> FreeLocations(StashGridClass grid, Item item, int firstRow)
    {
        var horizontalSize = item.CalculateRotatedSize(ItemRotation.Horizontal);
        var verticalSize = item.CalculateRotatedSize(ItemRotation.Vertical);
        bool distinctRotations = horizontalSize.X != verticalSize.X || horizontalSize.Y != verticalSize.Y;
        int lastRow = grid.GridHeight - Math.Min(horizontalSize.Y, verticalSize.Y);
        int lastColumn = grid.GridWidth - Math.Min(horizontalSize.X, verticalSize.X);
        for (int y = firstRow; y <= lastRow; y++)
        {
            for (int x = 0; x <= lastColumn; x++)
            {
                if (x + horizontalSize.X <= grid.GridWidth && y + horizontalSize.Y <= grid.GridHeight)
                {
                    var horizontal = new LocationInGrid(x, y, ItemRotation.Horizontal);
                    if (grid.method_0(item, horizontal))
                    {
                        yield return horizontal;
                    }
                }

                if (distinctRotations && x + verticalSize.X <= grid.GridWidth && y + verticalSize.Y <= grid.GridHeight)
                {
                    var vertical = new LocationInGrid(x, y, ItemRotation.Vertical);
                    if (grid.method_0(item, vertical))
                    {
                        yield return vertical;
                    }
                }
            }
        }
    }

    private static bool[] OccupiedRows(StashGridClass grid)
    {
        var occupied = new bool[grid.GridHeight];
        foreach (var item in grid.Items)
        {
            if (grid.GetItemLocation(item) is { } at)
            {
                int end = Math.Min(occupied.Length, at.y + item.CalculateRotatedSize(at.r).Y);
                for (int row = Math.Max(0, at.y); row < end; row++)
                {
                    occupied[row] = true;
                }
            }
        }

        return occupied;
    }
}
