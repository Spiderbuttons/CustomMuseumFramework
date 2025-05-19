using System;
using System.Linq;
using CustomMuseumFramework.Helpers;
using StardewModdingAPI;
using StardewValley;

namespace CustomMuseumFramework.Commands;

public class ResetVanillaCommand() : ConsoleCommand("resetvanilla")
{
    public override string GetDescription()
    {
        return $"{CommandHandler.BOLD + CommandHandler.UNDERLINE}cmf {Name}{CommandHandler.BOLD_OFF + CommandHandler.UNDERLINE_OFF}\r\n" +
               $"   Resets the vanilla museum by removing every donated item in its inventory.\r\n" +
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

        if (!ArgUtility.TryGetOptionalBool(args, 0, out var pop, out var error, defaultValue: true, name: "bool pop"))
        {
            Log.Error(error);
            return;
        }

        if (pop)
        {
            foreach (var (location, itemId) in Game1.netWorldState.Value.MuseumPieces.Pairs)
            {
                Game1.createItemDebris(ItemRegistry.Create($"(O){itemId}"), location * 64, 2,
                    Game1.RequireLocation("ArchaeologyHouse"));
            }
        }
        Game1.netWorldState.Value.MuseumPieces.Clear();
        Log.Info($"Reset the vanilla museum.");
    }
}