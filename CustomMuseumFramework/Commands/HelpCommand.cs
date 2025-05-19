using System.Linq;
using CustomMuseumFramework.Helpers;
using StardewValley;

namespace CustomMuseumFramework.Commands;

public class HelpCommand() : ConsoleCommand("help")
{
    public override string GetDescription()
    {
        return $"{CommandHandler.BOLD + CommandHandler.UNDERLINE}cmf {Name}{CommandHandler.BOLD_OFF + CommandHandler.UNDERLINE_OFF}\r\n" +
               $"   Provides information about Custom Museum Framework commands.\r\n" +
               $"   Usage: cmf {Name}\r\n" +
               $"      Lists all available Custom Museum Framework commands.\r\n" +
               $"   Usage: cmf {Name} <command>\r\n" +
               $"      Displays help for a specific command.\r\n" +
               $"      <command> - The name of the command to display help for.";
    }

    public override void Handle(string[] args)
    {
        var commands = CommandHandler.Commands;
        string text = "";

        if (!args.Any())
        {
            text = $"The 'cmf' command is the command prefix for Custom Museum Framework commands. To use a Custom Museum Framework command, type 'cmf' followed by the command name and any applicable arguments.\r\n\r\n" + 
                       $"Available commands:";
            foreach (var command in commands.Values)
            {
                text += $"\r\n\r\n" + 
                        $"{command.Description}";
            }
        } else if (commands.TryGetValue(args[0], out var command))
        {
            text = "\r\n" + command.GetDescription();
        }
        else
        {
            text = $"The 'cmf {args[0]}' command is not a valid Custom Museum Framework command.";
        }

        Log.Info(text.TrimEnd());
    }
}