using CustomMuseumFramework.Helpers;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.TokenizableStrings;

namespace CustomMuseumFramework;

public class TriggerActions
{
    public static bool DonateItem(string[] args, TriggerActionContext context, out string error)
    {
        if (!ArgUtility.TryGet(args, 1, out var museumId, out error, allowBlank: false, name: "string museumId") ||
            !ArgUtility.TryGet(args, 2, out var itemId, out error, allowBlank: false, name: "string itemId"))
        {
            return false;
        }
        
        if (!CMF.MuseumManagers.TryGetValue(museumId, out var museum))
        {
            error = "The museumId provided does not match an existing custom museum.";
            return false;
        }

        Vector2 location = museum.GetFreeDonationSpot().GetValueOrDefault();
        if (ArgUtility.HasIndex(args, 3) && !ArgUtility.TryGetVector2(args, 3, out location, out error, name: "vector2 tile"))
        {
            return false;
        }
        
        if (museum.DonateItem(location, itemId))
        {
            MultiplayerUtils.broadcastChatMessage(museum.ON_DONATION(TokenStringBuilder.ItemNameFor(ItemRegistry.Create(itemId))));
            return true;
        }

        return false;
    }

    public static bool ForceDonateItem(string[] args, TriggerActionContext context, out string error)
    {
        if (!ArgUtility.TryGet(args, 1, out var museumId, out error, allowBlank: false, name: "string museumId") ||
            !ArgUtility.TryGet(args, 2, out var itemId, out error, allowBlank: false, name: "string itemId"))
        {
            return false;
        }
        
        if (!CMF.MuseumManagers.TryGetValue(museumId, out var museum))
        {
            error = "The museumId provided does not match an existing custom museum.";
            return false;
        }

        Vector2 location = museum.GetFreeDonationSpot().GetValueOrDefault();
        if (ArgUtility.HasIndex(args, 3) && !ArgUtility.TryGetVector2(args, 3, out location, out error, name: "vector2 tile"))
        {
            return false;
        }
        
        if (museum.DonateItem(location, itemId, force: true))
        {
            MultiplayerUtils.broadcastChatMessage(museum.ON_DONATION(TokenStringBuilder.ItemNameFor(ItemRegistry.Create(itemId))));
            return true;
        }

        return false;
    }

    public static bool RemoveDonation(string[] args, TriggerActionContext context, out string error)
    {
        if (!ArgUtility.TryGet(args, 1, out var museumId, out error, allowBlank: false, name: "string museumId") ||
            !ArgUtility.TryGet(args, 2, out var itemId, out error, allowBlank: false, name: "string itemId") ||
            !ArgUtility.TryGetOptionalBool(args, 3, out var pop, out error, defaultValue: true, name: "bool pop"))
        {
            return false;
        }
        
        if (!CMF.MuseumManagers.TryGetValue(museumId, out var museum))
        {
            error = "The museumId provided does not match an existing custom museum.";
            return false;
        }

        return museum.RemoveItem(itemId, pop);
    }
}