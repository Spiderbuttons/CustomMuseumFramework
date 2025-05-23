using CustomMuseumFramework.Helpers;
using StardewValley;

namespace CustomMuseumFramework.Commands;

public class RetrieveCommand() : ConsoleCommand("retrieve")
{
    public override string GetDescription()
    {
        return
            $"{CommandHandler.BOLD + CommandHandler.UNDERLINE}{CommandHandler.RootCommand} {Name}{CommandHandler.BOLD_OFF + CommandHandler.UNDERLINE_OFF}\r\n" +
            $"   Open a chest menu containing the items in a given museum.\r\n" +
            $"   Usage: {CommandHandler.RootCommand} {Name} <museum>\r\n" +
            $"      <museum> - The id of the museum.\r\n";
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
        
        manager.OpenRetrievalMenu();
        Log.Info($"Opened retrieval menu for museum '{manager.Museum.Name}'");
    }
}