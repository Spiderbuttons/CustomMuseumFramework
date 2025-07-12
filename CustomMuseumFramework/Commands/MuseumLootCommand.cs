using CustomMuseumFramework.Helpers;
using StardewValley;

namespace CustomMuseumFramework.Commands;

public class MuseumLootCommand() : ConsoleCommand("museumloot")
{
    public override string GetDescription()
    {
        return
            $"{CommandHandler.RootCommand} {Name}\r\n" +
            $"   Fill your inventory with items yet to be donated to a specific museum.\r\n" +
            $"   Usage: {CommandHandler.RootCommand} {Name} <museum>\r\n" +
            $"      <museum> - The id of the museum the items should be donated to.\r\n";
    }

    public override void Handle(string[] args)
    {
        if (!ArgUtility.TryGet(args, 0, out var museumId, out var error, allowBlank: false, name: "string museumId"))
        {
            Log.Error(error);
            return;
        }
        
        if (!CMF.MuseumManagers.TryGetValue(museumId, out var manager))
        {
            Log.Error("The museum Id provided does not match an existing custom museum.");
            return;
        }

        int found = 0;
        int given = 0;

        foreach (var itemId in manager.TotalPossibleDonations)
        {
            if (manager.HasDonatedItem(itemId)) continue;
            found++;

            if (Game1.player.freeSpotsInInventory() > 0)
            {
                Game1.player.addItemToInventoryBool(ItemRegistry.Create(itemId));
                given++;
            }
        }
        
        Log.Info($"Filled inventory with {given} undonated items (out of {found} total) for museum '{manager.Museum.Name}'");
    }
}