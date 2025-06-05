using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;
using System.Collections.Generic;
using System.Linq;
using CustomMuseumFramework.Helpers;
using CustomMuseumFramework.Models;
using Microsoft.Xna.Framework.Graphics;
using StardewValley.Extensions;
using StardewValley.ItemTypeDefinitions;
using StardewValley.TokenizableStrings;
using StardewValley.Triggers;
using xTile.Dimensions;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace CustomMuseumFramework.Patches;

[HarmonyPatch(typeof(GameLocation))]
public static class GameLocationPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(GameLocation.updateEvenIfFarmerIsntHere))]
    public static void updateEvenIfFarmerIsntHere(GameLocation __instance)
    {
        if (!CMF.MuseumManagers.TryGetValue(__instance.Name, out var manager)) return;
        
        manager.Mutex.Update(__instance);
    }
    
    [HarmonyPostfix]
    [HarmonyPatch("resetLocalState")]
    public static void resetLocalState(GameLocation __instance)
    {
        if (!CMF.MuseumManagers.TryGetValue(__instance.Name, out var manager)) return;
        
        if (manager.DoesFarmerHaveAnythingToDonate(Game1.player))
        {
            Game1.player.mailReceived.Add($"{manager.Museum.Name}_SomethingToDonate");
        }
        
        if (manager.HasDonatedItem())
        {
            Game1.player.mailReceived.Add($"{manager.Museum.Name}_SomethingWasDonated");
        }
        
        foreach (var pair in manager.getLostBooksLocations())
        {
            if (!manager.Museum.modData.TryGetValue($"Spiderbuttons.CMF_LostBooks_{pair.Key}", out var bookTally) || !int.TryParse(bookTally, out var booksFound))
            {
                booksFound = 0;
            }
            
            for (int i = 0; i < pair.Value.Count; i++)
            {
                KeyValuePair<string, Vector2> bookLocation = pair.Value.ElementAt(i);
                string bookId = bookLocation.Key;
                Vector2 tile = bookLocation.Value;
                if (i + 1 <= booksFound && !Game1.player.mailReceived.Contains($"{manager.Museum.Name}_ReadLostBook_{pair.Key}_{bookId}"))
                {
                    manager.Museum.temporarySprites.Add(new TemporaryAnimatedSprite("LooseSprites\\Cursors",
                        new Rectangle(144, 447, 15, 15), new Vector2(tile.X * 64f, tile.Y * 64f - 96f - 16f),
                        flipped: false, 0f, Color.White)
                    {
                        interval = 99999f,
                        animationLength = 1,
                        totalNumberOfLoops = 9999,
                        yPeriodic = true,
                        yPeriodicLoopTime = 4000f,
                        yPeriodicRange = 16f,
                        layerDepth = 1f,
                        scale = 4f,
                        id = i
                    });
                }
            }
        }
    }
    
    [HarmonyPrefix]
    [HarmonyPatch(nameof(GameLocation.cleanupBeforePlayerExit))]
    public static void cleanupBeforePlayerExit(GameLocation __instance)
    {
        if (!CMF.MuseumManagers.TryGetValue(__instance.Name, out var manager)) return;
        
        manager._itemToRewardsLookup.Clear();
    }
    
    [HarmonyPrefix]
    [HarmonyPatch(nameof(GameLocation.checkAction))]
    public static bool checkAction(GameLocation __instance, Location tileLocation, xTile.Dimensions.Rectangle viewport, Farmer who, ref bool __result)
    {
        if (!CMF.MuseumManagers.TryGetValue(__instance.Name, out var manager)) return true;
        
        if (manager.DonatedItems.TryGetValue(new Vector2(tileLocation.X, tileLocation.Y), out var itemId) ||
            manager.DonatedItems.TryGetValue(new Vector2(tileLocation.X, tileLocation.Y - 1), out itemId))
        {
            ParsedItemData data = ItemRegistry.GetDataOrErrorItem(itemId);
            if (data.RawData?.GetType().GetField("CustomFields") is { } customFieldsField)
            {
                var customFields = customFieldsField.GetValue(data.RawData);
                if (customFields is Dictionary<string, string> fields && fields.TryGetValue("Spiderbuttons.CMF/PedestalAction", out var action))
                {
                    action = TokenParser.ParseText(string.Format(action, data.DisplayName, data.Description, manager.Museum.DisplayName,
                        data.QualifiedItemId));
                    if (!TriggerActionManager.TryRunAction(action, out var error, out _))
                    {
                        Log.Error(error);
                        return true;
                    }
                    
                    if (!fields.TryGetValue("Spiderbuttons.CMF/PedestalOverride", out var overrideAction) || !bool.TryParse(overrideAction, out var overrideResult) || overrideResult)
                    {
                        __result = true;
                        return false;
                    }
                }
            }

            InteractionData inter = manager.MuseumData.PedestalAction;
            if (inter.InteractionType is InteractionType.None || string.IsNullOrWhiteSpace(inter.Text))
            {
                __result = true;
                return false;
            }
            
            var text = TokenParser.ParseText(string.Format(inter.Text, data.DisplayName, data.Description, manager.Museum.DisplayName, data.QualifiedItemId));
            var customAction = inter.Action is not null
                ? TokenParser.ParseText(string.Format(inter.Action, data.DisplayName, data.Description, manager.Museum.DisplayName,
                    data.QualifiedItemId))
                : null;
            
            switch (inter.InteractionType)
            {
                case InteractionType.Sign:
                    Game1.drawObjectDialogue(text);
                    break;
                case InteractionType.Message:
                    Game1.drawDialogueNoTyping(text);
                    break;
                case InteractionType.Letter:
                    Game1.drawLetterMessage(text);
                    break;
                case InteractionType.None:
                    __result = true;
                    return false;
                case InteractionType.Custom when customAction is not null:
                    if (!TriggerActionManager.TryRunAction(customAction, out var error, out _))
                    {
                        Log.Error(error);
                        return true;
                    }
                    break;
                default:
                    Game1.drawObjectDialogue(text);
                    break;
            }
            
            if (inter.InteractionType is not InteractionType.Custom and not InteractionType.None && customAction is not null)
            {
                if (!TriggerActionManager.TryRunAction(customAction, out var error, out _))
                {
                    Log.Error(error);
                    return true;
                }
            }
            
            __result = true;
            return false;
        }

        return true;
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(nameof(GameLocation.draw))]
    public static void draw(GameLocation __instance, SpriteBatch b)
    {
        if (!CMF.MuseumManagers.TryGetValue(__instance.Name, out var manager)) return;
        
        foreach (KeyValuePair<Vector2, string> v in manager.DonatedItems)
        {
            ParsedItemData data = ItemRegistry.GetDataOrErrorItem(v.Value);
            b.Draw(Game1.shadowTexture, Game1.GlobalToLocal(Game1.viewport, v.Key * 64f + new Vector2(32f, 52f)),
                Game1.shadowTexture.Bounds, Color.White, 0f,
                new Vector2(Game1.shadowTexture.Bounds.Center.X, Game1.shadowTexture.Bounds.Center.Y), 4f,
                SpriteEffects.None, (v.Key.Y * 64f - 2f) / 10000f);

            var texture = data.GetTexture();
            var sourceRect = data.GetSourceRect();
            int textureOffset = data.GetSourceRect().Height - 16;
            b.Draw(texture, Game1.GlobalToLocal(Game1.viewport, v.Key * 64f) + new Vector2(0, -textureOffset * 4),
                sourceRect,
                Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, (v.Key.Y + 2f) * 64f / 10000f);
        }
    }
}