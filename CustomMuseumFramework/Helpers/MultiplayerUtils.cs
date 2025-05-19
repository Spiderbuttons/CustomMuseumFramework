using System;
using System.Linq;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.TokenizableStrings;
using StardewValley.Triggers;

namespace CustomMuseumFramework.Helpers;

public class MultiplayerUtils
{
    public struct TriggerPackage(string trigger, string? inputId, string? outputId, string? location)
    {
        public readonly string Trigger = trigger;
        public readonly string? InputId = inputId;
        public readonly string? TargetId = outputId;
        public readonly string? Location = location;
    }

    public static void broadcastTrigger(TriggerPackage trigger)
    {
        TriggerActionManager.Raise(trigger.Trigger, inputItem: ItemRegistry.Create(trigger.InputId, allowNull: true), targetItem: ItemRegistry.Create(trigger.TargetId, allowNull: true), location: Game1.getLocationFromName(trigger.Location));
        
        if (!Game1.IsMultiplayer || Game1.multiplayerMode == 0) return;
        
        CMF.ModHelper.Multiplayer.SendMessage(
            trigger,
            "Spiderbuttons.CMF_Trigger",
            modIDs: [CMF.Manifest.UniqueID]
        );
    }

    public static void receiveTrigger(object? _, ModMessageReceivedEventArgs e)
    {
        if (e.FromModID != CMF.Manifest.UniqueID || e.Type != "Spiderbuttons.CMF_Trigger") return;
        
        var trigger = e.ReadAs<TriggerPackage>();
        TriggerActionManager.Raise(trigger.Trigger, inputItem: ItemRegistry.Create(trigger.InputId, allowNull: true), targetItem: ItemRegistry.Create(trigger.TargetId, allowNull: true), location: Game1.getLocationFromName(trigger.Location));
    }
    
    public static void broadcastChatMessage(string text, params string[] subs)
    {
        if (!Game1.IsMultiplayer || Game1.multiplayerMode == 0) return;
        
        CMF.ModHelper.Multiplayer.SendMessage(
            new Tuple<string, string[]>(text, subs ?? []),
            "Spiderbuttons.CMF_ChatMessage",
            modIDs: [CMF.Manifest.UniqueID]
        );
        printChatMessage(text, subs ?? []);
    }

    public static void receiveChatMessage(object? _, ModMessageReceivedEventArgs e)
    {
        if (e.FromModID != CMF.Manifest.UniqueID || e.Type != "Spiderbuttons.CMF_ChatMessage") return;
        
        var msg = e.ReadAs<Tuple<string, string[]>>();
        printChatMessage(msg.Item1, msg.Item2);
    }

    public static void printChatMessage(string text, string[] subs)
    {
        if (Game1.chatBox is null) return;

        try
        {
            string[] processedSubs = subs.Select((string arg) => arg.StartsWith("aOrAn:") ? Utility.AOrAn(TokenParser.ParseText(arg.Substring("aOrAn:".Length))) : TokenParser.ParseText(arg)).ToArray();
            ChatBox chatBox = Game1.chatBox;
            object[] substitutions = new object[processedSubs.Length];
            for (int i = 0; i < processedSubs.Length; i++)
            {
                substitutions[i] = processedSubs[i];
            }
            chatBox.addInfoMessage(string.Format(text, substitutions));
        } catch
        {
            //
        }
    }
}