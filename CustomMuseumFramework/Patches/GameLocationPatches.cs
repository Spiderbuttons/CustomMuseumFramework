using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using StardewValley.ItemTypeDefinitions;
using StardewValley.TokenizableStrings;
using xTile.Dimensions;

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
        // TODO: Mail flag stuff later
        // if (!Game1.player.eventsSeen.Contains("0") && this.doesFarmerHaveAnythingToDonate(Game1.player))
        // {
        //     Game1.player.mailReceived.Add("somethingToDonate");
        // }
        // if (LibraryMuseum.HasDonatedArtifacts())
        // {
        //     Game1.player.mailReceived.Add("somethingWasDonated");
        // }
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
        if (text.Equals("MuseumMenu"))
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
                        __result = false;
                        return false;
                    }

                    manager.OpenMuseumDialogueMenu();
                    __result = true;
                    return false;
                }

                __result = false;
                return false;
            }

            manager.OpenMuseumDialogueMenu();
            __result = true;
            return false;
        }

        if (text.Equals("Rearrange"))
        {
            if (manager.HasDonatedItem())
            {
                string rearrangeText = manager.MuseumData.Strings.MenuRearrange ?? CMF.DefaultStrings.MenuRearrange ??
                                       Game1.content.LoadString("Strings\\Locations:ArchaeologyHouse_Rearrange");
                string retrieveText = manager.MuseumData.Strings.MenuRetrieve ?? CMF.DefaultStrings.MenuRetrieve ??
                                      Game1.content.LoadString("Strings\\Locations:ArchaeologyHouse_Gunther_Collect");
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
            Game1.drawObjectDialogue(Game1.parseText(" - " + data.DisplayName + " - " + "^" + data.Description));
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