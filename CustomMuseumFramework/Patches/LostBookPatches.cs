﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using CustomMuseumFramework.Helpers;
using CustomMuseumFramework.Models;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using Object = StardewValley.Object;

namespace CustomMuseumFramework.Patches;

[HarmonyPatch]
public static class LostBookPatches
{
    private static void foundCustomLostBook(Farmer farmer, string itemId)
    {
        if (!CMF.LostBookLookup.TryGetValue(itemId, out var manager) || !CMF.LostBookData.TryGetValue(manager.MuseumData.Id, out var bookList)) return;
        
        CustomLostBookData? data = bookList.FirstOrDefault(x => x.ItemId == itemId);
        if (data is null) return;
        
        bool shouldHoldUpArtifact = false;
        if (!farmer.hasOrWillReceiveMail($"{manager.Museum.Name}_FoundLostBook_{data.Id}"))
        {
            Game1.addMail($"{manager.Museum.Name}_FoundLostBook_{data.Id}", noLetter: true);
            shouldHoldUpArtifact = true;
        } else Game1.showGlobalMessage(data.ReceiveText ?? Game1.content.LoadString("Strings\\StringsFromCSFiles:FishingRod.cs.14100"));

        Game1.playSound("newRecipe");
        
        MultiplayerUtils.broadcastTrigger(new MultiplayerUtils.TriggerPackage("Spiderbuttons.CMF_BookFound", itemId, itemId, location: manager.Museum.Name));
        if (Context.IsMainPlayer) manager.IncrementLostBookCount(data.Id);
        else
        {
            CMF.ModHelper.Multiplayer.SendMessage(new[]{ manager.Museum.Name, data.Id }, "Spiderbuttons.CMF_LostBook", modIDs: [CMF.Manifest.UniqueID]);
        }
        
        farmer.stats.Increment($"Spiderbuttons.CMF_LostBooks_{data.Id}", 1);
        
        if (data.BroadcastText is null) Game1.Multiplayer.globalChatInfoMessage("LostBook", farmer.displayName);
        else MultiplayerUtils.broadcastChatMessage(data.BroadcastText, farmer.displayName);

        if (shouldHoldUpArtifact) farmer.holdUpItemThenMessage(ItemRegistry.Create(itemId));
    }

    public static void gotLostBookFromMenu(string? itemId, ItemGrabMenu igMenu, int x, int y)
    {
        if (itemId is null || !CMF.LostBookLookup.TryGetValue(itemId, out var manager) || !CMF.LostBookData.TryGetValue(manager.MuseumData.Id, out var bookList)) return;
        
        CustomLostBookData? data = bookList.FirstOrDefault(book => book.ItemId == itemId);
        if (data is null) return;

        igMenu.heldItem = null;
        foundCustomLostBook(Game1.player, itemId);
        igMenu.poof = new TemporaryAnimatedSprite("TileSheets\\animations", new Rectangle(0, 320, 64, 64), 50f, 8, 0, new Vector2(x - x % 64 + 16, y - y % 64 + 16), flicker: false, flipped: false);
        Game1.playSound("fireball");
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Farmer), nameof(Farmer.OnItemReceived))]
    public static void Farmer_OnItemReceived_Postfix(Farmer __instance, Item item, int countAdded, Item? mergedIntoStack, bool hideHudNotification)
    {
        if (!CMF.LostBookLookup.TryGetValue(item.QualifiedItemId, out var manager) || !CMF.LostBookData.TryGetValue(manager.MuseumData.Id, out var bookList)) return;

        Item actualItem = mergedIntoStack ?? item;
        CustomLostBookData? data = bookList.FirstOrDefault(x => x.ItemId == item.QualifiedItemId);
        if (data is null) return;
        
        Game1.PerformActionWhenPlayerFree(() => foundCustomLostBook(__instance, item.QualifiedItemId));
        
        __instance.removeItemFromInventory(actualItem);
        actualItem.HasBeenInInventory = true;
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Farmer), nameof(Farmer.GetItemReceiveBehavior))]
    public static void Farmer_GetItemReceiveBehavior_Postfix(Farmer __instance, Item item, ref bool needsInventorySpace, ref bool showNotification)
    {
        if (!CMF.LostBookLookup.TryGetValue(item.QualifiedItemId, out var manager) || !CMF.LostBookData.TryGetValue(manager.MuseumData.Id, out var bookList)) return;
        
        CustomLostBookData? data = bookList.FirstOrDefault(x => x.ItemId == item.QualifiedItemId);
        if (data is null) return;

        showNotification = true;
        needsInventorySpace = false;
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Farmer), nameof(Farmer.couldInventoryAcceptThisItem), [typeof(Item)])]
    public static void Farmer_couldInventoryAcceptThisItem_Postfix(Farmer __instance, ref bool __result, Item item)
    {
        if (!CMF.LostBookLookup.TryGetValue(item.QualifiedItemId, out var manager) || !CMF.LostBookData.TryGetValue(manager.MuseumData.Id, out var bookList)) return;

        CustomLostBookData? data = bookList.FirstOrDefault(x => x.ItemId == item.QualifiedItemId);
        if (data is null) return;

        __result = true;
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Farmer), nameof(Farmer.couldInventoryAcceptThisItem), [typeof(string), typeof(int), typeof(int)])]
    public static void Farmer_couldInventoryAcceptThisItem_Postfix(Farmer __instance, ref bool __result, string id, int stack, int quality = 0)
    {
        if (!CMF.LostBookLookup.TryGetValue(id, out var manager) || !CMF.LostBookData.TryGetValue(manager.MuseumData.Id, out var bookList)) return;

        CustomLostBookData? data = bookList.FirstOrDefault(x => x.ItemId == id);
        if (data is null) return;

        __result = true;
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Object), nameof(Object.checkForSpecialItemHoldUpMeessage))]
    public static void Object_checkForSpecialItemHoldUpMeessage_Postfix(Object __instance, ref string __result)
    {
        if (!CMF.LostBookLookup.TryGetValue(__instance.QualifiedItemId, out var manager) || !CMF.LostBookData.TryGetValue(manager.MuseumData.Id, out var bookList)) return;

        CustomLostBookData? data = bookList.FirstOrDefault(x => x.ItemId == __instance.QualifiedItemId);
        if (data is null) return;

        __result = data.ReceiveText ?? Game1.content.LoadString("Strings\\StringsFromCSFiles:Object.cs.12994");
    }
    
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(ItemGrabMenu), nameof(ItemGrabMenu.receiveLeftClick))]
    public static IEnumerable<CodeInstruction> ItemGrabMenu_receiveLeftClick_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
    {
        var code = instructions.ToList();
        try
        {
            var matcher = new CodeMatcher(code, il);

            matcher.MatchEndForward(
                new CodeMatch(OpCodes.Call, AccessTools.PropertyGetter(typeof(Item), nameof(Item.QualifiedItemId))),
                new CodeMatch(op => op.IsStloc()),
                new CodeMatch(op => op.IsLdloc()),
                new CodeMatch(OpCodes.Ldstr, "(O)326")
            );

            var loc = matcher.InstructionAt(-1).operand;

            matcher.Insert(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(LostBookPatches), nameof(gotLostBookFromMenu))),
                new CodeInstruction(OpCodes.Ldloc_S, loc)
            );

            return matcher.InstructionEnumeration();
        }
        catch (Exception ex)
        {
            Log.Error("Error in CMF.LostBookPatches_ItemGrabMenu_receiveLeftClick_Transpiler: \n" + ex);
            return code;
        }
    }
}