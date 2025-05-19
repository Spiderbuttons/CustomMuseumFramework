using System.Collections.Generic;
using System.Linq;
using CustomMuseumFramework.Helpers;

namespace CustomMuseumFramework.Commands;

public static class CommandHandler
{
    public const string BOLD = "\x1b[1m";
    public const string BOLD_OFF = "\x1b[22m";
    public const string UNDERLINE = "\x1b[4m";
    public const string UNDERLINE_OFF = "\x1b[24m";
    
    public static Dictionary<string, ConsoleCommand> Commands { get; } = new()
    {
        { "help", new HelpCommand() },
        { "reset", new ResetCommand() },
        { "resetall", new ResetAllCommand() },
        { "resetvanilla", new ResetVanillaCommand() }
    };
    
    public static void Handle(string[] args)
    {
        string command = args.FirstOrDefault() ?? "help";
        string[] commandArgs = args.Skip(1).ToArray();
        if (Commands.TryGetValue(command, out var handler))
        {
            handler.Handle(commandArgs);
            return;
        }
        Log.Error($"The 'cmf {command}' command is not a valid Custom Museum Framework command.");
    }
}