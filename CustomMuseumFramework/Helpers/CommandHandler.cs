using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StardewModdingAPI;

namespace CustomMuseumFramework.Helpers;

[Command]
public abstract class ConsoleCommand(string name, bool allowedOnTitle = false)
{
    public string Name { get; } = name;
    public bool AllowedOnTitle = allowedOnTitle;
    public string Description => GetDescription();

    public abstract string GetDescription();
    public abstract void Handle(string[] args);
}

[AttributeUsage(AttributeTargets.Class)]
public class CommandAttribute : Attribute;

public class CommandHandler
{
    public CommandHandler(IModHelper helper, IManifest manifest, string rootCommand)
    {
        CommandHelper = helper.ConsoleCommands;
        ModName = manifest.Name;
        RootCommand = rootCommand;

        Register();
    }

    public const string BOLD = "\x1b[1m";
    public const string BOLD_OFF = "\x1b[22m";
    public const string UNDERLINE = "\x1b[4m";
    public const string UNDERLINE_OFF = "\x1b[24m";

    private ICommandHelper CommandHelper { get; }
    public static string ModName { get; set; } = string.Empty;
    public static string RootCommand { get; set; } = string.Empty;

    public static Dictionary<string, ConsoleCommand> Commands { get; } = new();

    private void Register()
    {
        var comms = typeof(CommandHandler).Assembly.GetTypes().Where(type => type is { IsClass: true, IsAbstract: false } && type.GetCustomAttribute(typeof(CommandAttribute)) != null);
        foreach (var type in comms)
        {
            var instance = (ConsoleCommand)Activator.CreateInstance(type)!;
            Commands.Add(instance.Name, instance);
        }
        
        CommandHelper.Add(RootCommand, $"Starts a {ModName} command.", (_, args) => Handle(args));
    }
    
    public void Handle(string[] args)
    {
        string command = args.FirstOrDefault() ?? "help";
        string[] commandArgs = args.Skip(1).ToArray();
        if (Commands.TryGetValue(command, out var handler))
        {
            if (!handler.AllowedOnTitle && !Context.IsWorldReady)
            {
                Log.Error($"This command can only be used when a save is loaded.");
                return;
            }
            
            handler.Handle(commandArgs);
            return;
        }
        Log.Error($"The '{RootCommand} {command}' command is not a valid {ModName} command.");
    }
}

public class GenericHelpCommand() : ConsoleCommand("help", allowedOnTitle: true)
{
    public override string GetDescription()
    {
        return $"{CommandHandler.BOLD + CommandHandler.UNDERLINE}{CommandHandler.RootCommand} {Name}{CommandHandler.BOLD_OFF + CommandHandler.UNDERLINE_OFF}\r\n" +
               $"   Provides information about {CommandHandler.ModName} commands.\r\n" +
               $"   Usage: cmf {Name}\r\n" +
               $"      Lists all available {CommandHandler.ModName} commands.\r\n" +
               $"   Usage: cmf {Name} <command>\r\n" +
               $"      Displays help for a specific command.\r\n" +
               $"      <command> - The name of the command to display help for.";
    }

    public override void Handle(string[] args)
    {
        var commands = CommandHandler.Commands;
        string text;

        if (!args.Any())
        {
            text = $"The '{CommandHandler.RootCommand}' command is the command prefix for {CommandHandler.ModName} commands. To use a {CommandHandler.ModName} command, type '{CommandHandler.RootCommand}' followed by the command name and any applicable arguments.\r\n\r\n" + 
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
            text = $"The '{CommandHandler.RootCommand} {args[0]}' command is not a valid {CommandHandler.ModName} command.";
        }

        Log.Info(text.TrimEnd());
    }
}