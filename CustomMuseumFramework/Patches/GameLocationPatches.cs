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
            Game1.player.mailReceived.Add($"{manager.Museum.Name}_somethingToDonate");
        }
        
        if (manager.HasDonatedItem())
        {
            Game1.player.mailReceived.Add($"{manager.Museum.Name}_somethingWasDonated");
        }
        
        foreach (var pair in manager.getLostBooksLocations())
        {
            if (!manager.Museum.modData.TryGetValue($"Spiderbuttons.CMF_LostBooks_{pair.Key}", out var bookTally) || !int.TryParse(bookTally, out var booksFound))
            {
                booksFound = 0;
            }

            foreach (var bookLocation in pair.Value)
            {
                int index = bookLocation.Key;
                Vector2 tile = bookLocation.Value;
                if (index + 1 <= booksFound && !Game1.player.mailReceived.Contains($"{manager.Museum.Name}_ReadLostBook_${pair.Key}_{index}"))
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
                        id = index
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
    [HarmonyPatch(nameof(GameLocation.performAction), [typeof(string[]), typeof(Farmer), typeof(Location)])]
    public static bool performAction(GameLocation __instance, string[] action, Farmer who, Location tileLocation, ref bool __result)
    {
        if (!CMF.MuseumManagers.TryGetValue(__instance.Name, out var manager)) return true;
        
        if (!who.IsLocalPlayer) return true;

        string text = ArgUtility.Get(action, 0);

        if (text.EqualsIgnoreCase("Spiderbuttons.CMF_LostBook"))
        {
            if (!CMF.LostBookData.TryGetValue(manager.MuseumData.Id, out var bookList) || !bookList.Any()) return true;
            string bookDataId = ArgUtility.Get(action, 1);
            int bookDataIndex = ArgUtility.GetInt(action, 2);

            var bookData = bookList.FirstOrDefault(book => book.Id.EqualsIgnoreCase(bookDataId));
            if (bookData is null)
            {
                Log.Warn($"No LostBook data with Id '{bookDataId}' found for museum '{manager.Museum.Name}'.");
                __result = false;
                return false;
            }
            
            if (bookData.Entries.Count <= bookDataIndex || bookDataIndex < 0)
            {
                Log.Warn($"LostBook data with Id '{bookDataId}' has no entry at index '{bookDataIndex}' for museum '{manager.Museum.Name}'.");
                __result = false;
                return false;
            }
            
            if (!manager.Museum.modData.TryGetValue($"Spiderbuttons.CMF_LostBooks_{bookDataId}", out var bookTally) || !int.TryParse(bookTally, out var booksFound))
            {
                booksFound = 0;
            }

            if (bookDataIndex >= booksFound)
            {
                __result = false;
                return false;
            }

            var entry = bookData.Entries[bookDataIndex];
            
            switch (entry.InteractionType)
            {
                case InteractionType.Sign:
                    Game1.drawObjectDialogue(entry.Text);
                    break;
                case InteractionType.Message:
                    Game1.drawDialogueNoTyping(entry.Text);
                    break;
                case InteractionType.Letter:
                    Game1.drawLetterMessage(entry.Text);
                    break;
                case InteractionType.None:
                    break;
                case InteractionType.Custom when entry.Action is not null:
                    if (!TriggerActionManager.TryRunAction(TokenParser.ParseText(entry.Action), out var error, out _))
                    {
                        Log.Error(error);
                        return true;
                    }
                    break;
                default:
                    Game1.drawLetterMessage(entry.Text);
                    break;
            }
            
            if (!Game1.player.hasOrWillReceiveMail($"{manager.Museum.Name}_ReadLostBook_${bookDataId}_{bookDataIndex}"))
            {
                // We can't just remove sprites by checking their id alone because books from different sets will share numeric IDs
                // (pls give us string IDs or some other way to identify TASes in future Stardew Versions i beg u)
                // So we need to check that the sprite is in the right location that we'd expect, too... roughly.)
                
                Game1.player.mailReceived.Add($"{manager.Museum.Name}_ReadLostBook_${bookDataId}_{bookDataIndex}");
            
                Vector2 spriteLocation = new Vector2(tileLocation.X * 64f, tileLocation.Y * 64f - 96f - 16f);
                TemporaryAnimatedSprite? sprite = manager.Museum.temporarySprites.FirstOrDefault(s => s.id == bookDataIndex && Math.Abs(s.position.X - spriteLocation.X) < 1 && s.position.Y <= spriteLocation.Y + 16.1f && s.position.Y >= spriteLocation.Y - 16.1f);
                if (sprite is not null)
                {
                    sprite.destroyable = true;
                    sprite.alpha = 0f;
                    sprite.scale = 0f;
                }
            }

            __result = true;
            return false;
        }
        
        if (text.EqualsIgnoreCase("Spiderbuttons.CMF_MuseumMenu"))
        {
            if (manager.MuseumData.Owner?.RequiredForDonation is true)
            {
                foreach (NPC npc in __instance.characters)
                {
                    if (!npc.Name.Equals(manager.MuseumData.Owner.Name)) continue;
                    if (manager.MuseumData.Owner.Area is null || manager.MuseumData.Owner.Area.Value.IsEmpty)
                    {
                        manager.OpenMuseumDialogueMenu();
                        __result = true;
                        return false;
                    }

                    if (!manager.IsNpcClockedIn(npc, manager.MuseumData.Owner.Area.Value))
                    {
                        string clockedText = manager.MuseumData.Strings.ClockedOut ?? i18n.ClockedOut();
                        Game1.drawObjectDialogue(TokenParser.ParseText(clockedText));
                    } else manager.OpenMuseumDialogueMenu();
                    
                    __result = true;
                    return false;
                }
                
                // TODO: Allow collection of rewards without owner present.
                // TODO: Display a customizable string when the owner is clocked out.

                __result = false;
                return false;
            }

            manager.OpenMuseumDialogueMenu();
            __result = true;
            return false;
        }

        if (text.EqualsIgnoreCase("Spiderbuttons.CMF_Rearrange") && !manager.Mutex.IsLocked())
        {
            if (manager.HasDonatedItem())
            {
                string rearrangeText = manager.MuseumData.Strings.MenuRearrange ?? i18n.MenuRearrange();
                string retrieveText = manager.MuseumData.Strings.MenuRetrieve ?? i18n.MenuRetrieve();
                Response[] choice = manager.MuseumData.AllowRetrieval && !manager.Mutex.IsLocked() ? new Response[3]
                {
                    new Response("Rearrange", TokenParser.ParseText(rearrangeText)),
                    new Response("Retrieve", TokenParser.ParseText(retrieveText)),
                    new Response("Leave", Game1.content.LoadString("Strings\\Locations:ArchaeologyHouse_Gunther_Leave"))
                } : new Response[2]
                {
                    new Response("Rearrange", TokenParser.ParseText(rearrangeText)),
                    new Response("Leave", Game1.content.LoadString("Strings\\Locations:ArchaeologyHouse_Gunther_Leave"))
                };
                __instance.createQuestionDialogue("", choice, "Museum_Rearrange");
            }

            __result = true;
            return false;
        }

        return true;
    }
    
    [HarmonyPrefix]
    [HarmonyPatch(nameof(GameLocation.answerDialogueAction))]
    public static bool answerDialogueAction(GameLocation __instance, string? questionAndAnswer, string[] questionParams, ref bool __result)
    {
        if (!CMF.MuseumManagers.TryGetValue(__instance.Name, out var manager)) return true;
        
        if (questionAndAnswer is null)
        {
            __result = false;
            return false;
        }

        switch (questionAndAnswer)
        {
            case "Museum_Collect":
                manager.OpenRewardMenu();
                break;
            case "Museum_Donate":
                manager.OpenDonationMenu();
                break;
            case "Museum_Rearrange_Rearrange":
                manager.OpenRearrangeMenu();
                break;
            case "Museum_Rearrange_Retrieve":
                manager.OpenRetrievalMenu();
                break;
        }

        return true;
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
                    break;
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