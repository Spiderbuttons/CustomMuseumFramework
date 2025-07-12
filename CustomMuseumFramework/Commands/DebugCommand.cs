using CustomMuseumFramework.Helpers;
using StardewModdingAPI;
using StardewValley;

namespace CustomMuseumFramework.Commands;

public class DebugCommand() : ConsoleCommand("debug")
{
    public override string GetDescription()
    {
        return $"{CommandHandler.RootCommand} {Name}\r\n" +
               $"   Display some debug information about a custom museum.\r\n" +
               $"   Usage: {CommandHandler.RootCommand} {Name} <museum>\r\n" +
               $"      <museum> - The id of the museum to display information for.";
    }

    public override void Handle(string[] args)
    {
        if (!Context.IsWorldReady)
        {
            Log.Error($"This command can only be used when a save is loaded.");
            return;
        }

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

        var debugString = $"{manager.Museum.DisplayName} ({manager.MuseumData.Id}):\r\n" +
                          $"   {PossibleDonations(manager)}\r\n" +
                          $"   {CurrentDonations(manager)}\r\n" +
                          $"   {Pedestals(manager)}";
        Log.Info(debugString);
    }
    
    private string PossibleDonations(MuseumManager manager)
    {
        return $"Total Possible Donations: {manager.TotalPossibleDonations.Count}.";
    }

    private string CurrentDonations(MuseumManager manager)
    {
        return $"Current Donations: {manager.DonatedItems.Count} ({manager.ValidDonatedItems.Count} valid, {manager.DonatedItems.Count - manager.ValidDonatedItems.Count} invalid).";
    }

    private string Pedestals(MuseumManager manager)
    {
        var pedestalCount = 0;
        int mapWidth = manager.Museum.Map.DisplayWidth / Game1.tileSize;
        int mapHeight = manager.Museum.Map.DisplayHeight / Game1.tileSize;
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                pedestalCount += manager.IsTileDonationSpot(x, y) ? 1 : 0;
            }
        }
        
        int completionTarget = manager.MuseumData.CompletionNumber ?? manager.TotalPossibleDonations.Count;
        return $"Item Pedestals: {pedestalCount} ({completionTarget} total required for completion).";
    }
}