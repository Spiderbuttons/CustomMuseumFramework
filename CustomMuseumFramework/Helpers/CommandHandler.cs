using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StardewModdingAPI;

namespace CustomMuseumFramework.Helpers;

[Command]
public abstract class ConsoleCommand(string name, bool allowOnTitle = false)
{
    public readonly string Name = name;
    public readonly bool AllowOnTitle = allowOnTitle;
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
    }

    private ICommandHelper CommandHelper { get; }
    public static string ModName { get; set; } = string.Empty;
    public static string RootCommand { get; set; } = string.Empty;

    public static Dictionary<string, ConsoleCommand> Commands { get; } = new();

    public void Register()
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
            if (!handler.AllowOnTitle && !Context.IsWorldReady)
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

public class GenericHelpCommand() : ConsoleCommand("help", allowOnTitle: true)
{
    public override string GetDescription()
    {
        return $"{CommandHandler.RootCommand} {Name}\r\n" +
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