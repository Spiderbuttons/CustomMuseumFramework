using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CustomMuseumFramework.Helpers;
using CustomMuseumFramework.Models;
using StardewValley;
using StardewValley.ItemTypeDefinitions;

namespace CustomMuseumFramework;

public class GlobalDonatableCache : Dictionary<string, DonationInfoCache>
{
    private readonly Dictionary<string, List<string>> AssetNameTypeLookup = new()
    {
        { "Data/Objects", ["(O)"] },
        { "Data/Furniture", ["(F)"] },
        { "Data/BigCraftables", ["(BC)"] },
        { "Data/Boots", ["(B)"] },
        { "Data/AdditionalWallpaperFlooring", ["(WP)", "(FL)"] },
        { "Data/FloorsAndPaths", ["(WP)", "(FL)"] },
        { "Data/Hats", ["(H)"] },
        { "Data/Mannequins", ["(M)"] },
        { "Data/Pants", ["(P)"] },
        { "Data/Shirts", ["(S)"] },
        { "Data/Tools", ["(T)"] },
        { "Data/Trinkets", ["(TR)"] },
        { "Data/Weapons", ["(W)"] }
    };

    public void Invalidate(string assetName)
    {
        if (!AssetNameTypeLookup.TryGetValue(assetName, out var types))
        {
            return;
        }
        
        Log.Trace("Invalidating donation cache for asset: " + assetName);

        var clearTotals = false;
        foreach (var type in types)
        {
            if (!ContainsKey(type)) continue;
            
            if (this[type].IsUsed) clearTotals = true;
            Remove(type);
        }

        if (!clearTotals) return;
        foreach (var manager in CMF.MuseumManagers.Values)
        {
            manager.TotalPossibleDonations.Clear();
        }
    }

    public void InvalidateAll()
    {
        Clear();
        foreach (var manager in CMF.MuseumManagers.Values)
        {
            manager.TotalPossibleDonations.Clear();
        }
    }

    public new bool TryGetValue(string itemType, [MaybeNullWhen(false)] out DonationInfoCache cache)
    {
        if (this.ContainsKey(itemType))
        {
            cache = base[itemType];
            return true;
        }

        if (ItemRegistry.GetTypeDefinition(itemType) is not { } itemDataDefinition)
        {
            cache = null;
            return false;
        }

        cache = new DonationInfoCache(itemDataDefinition);
        this[itemType] = cache;
        cache.BuildCache();
        return true;
    }
}

public class DonationInfoCache(IItemDataDefinition ItemType) : Dictionary<string, SortedList<MuseumManager, DonationInfo>>
{
    public bool IsUsed;
    
    public void BuildCache()
    {
        foreach (var itemId in ItemType.GetAllIds())
        {
            Item item = ItemRegistry.Create($"{ItemType.Identifier}{itemId}");

            foreach (var manager in CMF.MuseumManagers)
            {
                var museum = manager.Value;
                DonationInfo info = new DonationInfo(
                    museum.IsItemSuitableForDonation(item, checkDonatedItems: false, firstPass: true),
                    museum.HasDonatedItem(item.QualifiedItemId));

                if (!ContainsKey(item.QualifiedItemId))
                {
                    this[item.QualifiedItemId] = new SortedList<MuseumManager, DonationInfo>(new MuseumManagerComparer());
                }

                this[item.QualifiedItemId].TryAdd(museum, info);
                if (info.IsValidDonation)
                {
                    museum.AddPossibleDonation(item);
                    IsUsed = true;
                }
            }
        }
    }
}