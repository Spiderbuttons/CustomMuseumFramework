using CustomMuseumFramework.Helpers;
using StardewModdingAPI;
using StardewValley;

namespace CustomMuseumFramework.Commands;

public class ResetAllCommand() : ConsoleCommand("resetall")
{
    public override string GetDescription()
    {
        return $"{CommandHandler.BOLD + CommandHandler.UNDERLINE}cmf {Name}{CommandHandler.BOLD_OFF + CommandHandler.UNDERLINE_OFF}\r\n" +
               $"   Resets all museums by removing every donated item in their inventories.\r\n" +
               $"   Usage: cmf {Name} [pop]\r\n" +
               $"      [pop] - Whether to pop the items onto the ground (true) or delete them (false). Default: true";
    }

    public override void Handle(string[] args)
    {
        if (!Context.IsWorldReady)
        {
            Log.Error($"This command can only be used when a save is loaded.");
            return;
        }

        if (!ArgUtility.TryGetOptionalBool(args, 1, out var pop, out var error, defaultValue: true, name: "bool pop"))
        {
            Log.Error(error);
            return;
        }

        foreach (var museum in CMF.MuseumManagers.Values)
        {
            museum.Reset(pop);
        }
        Log.Info($"Reset all museums.");
    }
}