using CustomMuseumFramework.Helpers;
using StardewModdingAPI;
using StardewValley;

namespace CustomMuseumFramework.Commands;

public class ResetCommand() : ConsoleCommand("reset")
{
    public override string GetDescription()
    {
        return $"cmf {Name}\r\n" +
               $"   Resets a museum by removing every donated item in its inventory.\r\n" +
               $"   Usage: cmf {Name} <museum> [pop]\r\n" +
               $"      <museum> - The id of the museum to reset.\r\n" +
               $"      [pop] - Whether to pop the items onto the ground (true) or delete them (false). Default: true";
    }

    public override void Handle(string[] args)
    {
        if (!Context.IsWorldReady)
        {
            Log.Error($"This command can only be used when a save is loaded.");
            return;
        }

        if (!ArgUtility.TryGet(args, 0, out var museumId, out var error, allowBlank: false, name: "string museumId") ||
            !ArgUtility.TryGetOptionalBool(args, 1, out var pop, out error, defaultValue: true, name: "bool pop"))
        {
            Log.Error(error);
            return;
        }
        
        if (!CMF.MuseumManagers.TryGetValue(museumId, out var museum))
        {
            Log.Error("The museum Id provided does not match an existing custom museum.");
            return;
        }

        museum.Reset(pop);
        Log.Info($"Reset museum '{museumId}.'");
    }
}