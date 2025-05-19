using HarmonyLib;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Extensions;

namespace CustomMuseumFramework;

public class Queries
{
    public static bool MUSEUM_DONATIONS(string[] query, GameStateQueryContext context)
    {
        if (!ArgUtility.TryGet(query, 1, out var museumId, out var error, allowBlank: false,name: "string museumId") ||
            !ArgUtility.TryGetOptionalInt(query, 2, out var min, out error, defaultValue: 1, name: "int minCount") ||
            !ArgUtility.TryGetOptionalInt(query, 3, out var max, out error, defaultValue: -1, name: "int maxCount") ||
            !ArgUtility.TryGetOptionalRemainder(query, 4, out var remainder, defaultValue: "", delimiter: ' '))
        {
            return GameStateQuery.Helpers.ErrorResult(query, error);
        }

        if (!CMF.MuseumManagers.TryGetValue(museumId, out var museum))
        {
            return GameStateQuery.Helpers.ErrorResult(query, "The museumId provided does not match an existing museum.");
        }

        if (!ArgUtility.HasIndex(query, 2)) return museum.HasDonatedItem();
        
        if (max == -1) max = int.MaxValue;
        
        if (string.IsNullOrEmpty(remainder)) 
        {
            return museum.DonatedItems.Count >= min && museum.DonatedItems.Count <= max;
        }

        int count = 0;
        GameStateQuery.Helpers.AnyArgMatches(query, 4, req =>
        {
            var reqData = museum.MuseumData.DonationRequirements.Find(x => x.Id.EqualsIgnoreCase(req));
            if (reqData == null) return false;
            count += museum.DonationsSatisfyingRequirement(reqData);
            return true;
        });

        return count >= min && count <= max;
    }
}