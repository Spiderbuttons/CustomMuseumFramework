using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Extensions;
using StardewValley.Internal;

namespace CustomMuseumFramework;

public class Queries
{
    public static bool MUSEUM_DONATIONS(string[] query, GameStateQueryContext context)
    {
        if (!ArgUtility.TryGet(query, 1, out var museumId, out var error, allowBlank: false, name: "string museum Id") ||
            !ArgUtility.TryGetOptionalInt(query, 2, out var min, out error, defaultValue: 1, name: "int min") ||
            !ArgUtility.TryGetOptionalInt(query, 3, out var max, out error, defaultValue: int.MaxValue, name: "int max") ||
            !ArgUtility.TryGetOptionalRemainder(query, 4, out var remainder, defaultValue: "", delimiter: ' '))
        {
            return GameStateQuery.Helpers.ErrorResult(query, error);
        }

        if (!CMF.MuseumManagers.TryGetValue(museumId, out var manager))
        {
            return GameStateQuery.Helpers.ErrorResult(query, "The museum Id provided does not match an existing custom museum");
        }

        if (!ArgUtility.HasIndex(query, 2)) return manager.HasDonatedItem();
        
        if (max == -1) max = int.MaxValue;
        
        if (string.IsNullOrWhiteSpace(remainder)) 
        {
            return manager.ValidDonatedItems.Count >= min && manager.ValidDonatedItems.Count <= max;
        }

        int count = 0;
        GameStateQuery.Helpers.AnyArgMatches(query, 4, req =>
        {
            var reqData = manager.MuseumData.DonationRequirements.Find(x => x.Id.EqualsIgnoreCase(req));
            if (reqData == null) return false;
            count += manager.DonationsSatisfyingRequirement(reqData);
            return true;
        });

        return count >= min && count <= max;
    }

    public static bool MUSEUM_HAS_ITEM(string[] query, GameStateQueryContext context)
    {
        if (!ArgUtility.TryGet(query, 1, out var museumId, out var error, allowBlank: false, name: "string museum Id"))
        {
            return GameStateQuery.Helpers.ErrorResult(query, error);
        }
        
        if (!CMF.MuseumManagers.TryGetValue(museumId, out var manager))
        {
            return GameStateQuery.Helpers.ErrorResult(query, "The museum Id provided does not match an existing custom museum");
        }
        
        if (!ArgUtility.HasIndex(query, 2)) return manager.HasDonatedItem();
        
        return GameStateQuery.Helpers.AnyArgMatches(query, 2, itemId => manager.HasDonatedItem(ItemRegistry.QualifyItemId(itemId)));
    }

    public static bool IS_ITEM_DONATED(string[] query, GameStateQueryContext context)
    {
        if (!ArgUtility.TryGet(query, 1, out _, out var error, allowBlank: false, name: "string item Id"))
        {
            return GameStateQuery.Helpers.ErrorResult(query, error);
        }

        return GameStateQuery.Helpers.AnyArgMatches(query, 1, id =>
        {
            return CMF.GlobalDonatableCache.TryGetValue(ItemRegistry.Create(id).TypeDefinitionId, out var cache) && cache.TryGetValue(id, out var museums) && museums.Any(museum => museum.Value.IsDonated);
        });
    }

    public static IEnumerable<ItemQueryResult> LOST_BOOK_OR_ITEM(string key, string arguments, ItemQueryContext context, bool avoidRepeat, HashSet<string> avoidItemIds, Action<string, string> logError)
    {
        var args = ItemQueryResolver.Helpers.SplitArguments(arguments);
        if (!ArgUtility.TryGet(args, 0, out var museumId, out var error, allowBlank: false, name: "string museum Id") ||
            !ArgUtility.TryGet(args, 1, out var booksetId, out error, allowBlank: false, name: "string booksetId"))
        {
            ItemQueryResolver.Helpers.ErrorResult(key, arguments, logError, error);
            return Array.Empty<ItemQueryResult>();
        }
        
        if (!CMF.MuseumManagers.TryGetValue(museumId, out var manager))
        {
            ItemQueryResolver.Helpers.ErrorResult(key, arguments, logError, "The museum Id provided does not match an existing custom museum");
            return Array.Empty<ItemQueryResult>();
        }
        
        if (!CMF.LostBookData.TryGetValue(museumId, out var bookList) || !bookList.Any())
        {
            ItemQueryResolver.Helpers.ErrorResult(key, arguments, logError, $"The museum with Id '{museumId}' does not have any lost book data");
            return Array.Empty<ItemQueryResult>();
        }
        
        var bookset = bookList.FirstOrDefault(bookset => bookset.Id.EqualsIgnoreCase(booksetId));
        if (bookset == null)
        {
            ItemQueryResolver.Helpers.ErrorResult(key, arguments, logError, $"The museum '{manager.Museum.Name}' does not have any lost book data with Id '{booksetId}'");
            return Array.Empty<ItemQueryResult>();
        }
        
        if (manager.FoundBooksFromBookset(booksetId) < manager.TotalBooksInBookset(booksetId))
        {
            return new ItemQueryResult[]
            {
                new(ItemRegistry.Create(bookset.ItemId))
            };
        }

        var fallback = string.Join(" ", args.Skip(2));
        
        if (string.IsNullOrWhiteSpace(fallback))
        {
            return Array.Empty<ItemQueryResult>();
        }
        return ItemQueryResolver.TryResolve(fallback, new ItemQueryContext(context, $"query '{CMF.Manifest.UniqueID}_LOST_BOOK_OR_ITEM'"));
    }
    
    public static bool LOST_BOOKS_FOUND(string[] query, GameStateQueryContext context)
    {
        if (!ArgUtility.TryGet(query, 1, out var museumId, out var error, allowBlank: false, name: "string museumId") ||
            !ArgUtility.TryGet(query, 2, out var booksetId, out error, allowBlank: false, name: "string lost book set Id") ||
            !ArgUtility.TryGetOptionalInt(query, 3, out var min, out error, defaultValue: 1, name: "int min") ||
            !ArgUtility.TryGetOptionalInt(query, 4, out var max, out error, defaultValue: int.MaxValue, name: "int max"))
        {
            return GameStateQuery.Helpers.ErrorResult(query, error);
        }

        if (!CMF.MuseumManagers.TryGetValue(museumId, out var manager))
        {
            return GameStateQuery.Helpers.ErrorResult(query, "The museum Id provided does not match an existing custom museum");
        }

        if (!CMF.LostBookData.TryGetValue(museumId, out var bookList) || !bookList.Any())
        {
            return GameStateQuery.Helpers.ErrorResult(query, $"The museum with Id '{museumId}' does not have any lost book data");
        }
        
        if (!bookList.Any(bookset => bookset.Id.EqualsIgnoreCase(booksetId)))
        {
            return GameStateQuery.Helpers.ErrorResult(query, $"The museum '{manager.Museum.Name}' does not have any lost book data with Id '{booksetId}'");
        }
        
        if (max == -1) max = int.MaxValue;
        if (!manager.Museum.modData.TryGetValue($"Spiderbuttons.CMF_LostBooks_{booksetId}", out var countString) ||
            !int.TryParse(countString, out var count)) return false;
        
        return count >= min && count <= max;
    }
    
    public static bool TOTAL_LOST_BOOKS_FOUND(string[] query, GameStateQueryContext context)
    {
        if (!ArgUtility.TryGet(query, 1, out var museumId, out var error, allowBlank: false, name: "string museumId") ||
            !ArgUtility.TryGetOptionalInt(query, 2, out var min, out error, defaultValue: 1, name: "int min") ||
            !ArgUtility.TryGetOptionalInt(query, 3, out var max, out error, defaultValue: int.MaxValue, name: "int max"))
        {
            return GameStateQuery.Helpers.ErrorResult(query, error);
        }

        if (!CMF.MuseumManagers.TryGetValue(museumId, out var manager))
        {
            return GameStateQuery.Helpers.ErrorResult(query, "The museum Id provided does not match an existing custom museum");
        }
        
        if (!CMF.LostBookData.TryGetValue(museumId, out var bookList) || !bookList.Any())
        {
            return GameStateQuery.Helpers.ErrorResult(query, $"The museum with Id '{museumId}' does not have any lost book data");
        }
        
        if (max == -1) max = int.MaxValue;
        if (!manager.Museum.modData.TryGetValue($"Spiderbuttons.CMF_TotalLostBooks", out var countString) ||
            !int.TryParse(countString, out var count)) return false;
        
        return count >= min && count <= max;
    }
}