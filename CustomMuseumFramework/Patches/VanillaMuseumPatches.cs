using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using CustomMuseumFramework.Helpers;
using HarmonyLib;
using StardewValley;
using StardewValley.Menus;

namespace CustomMuseumFramework.Patches;

[HarmonyPatch]
public static class VanillaMuseumPatches
{
    public static void vanillaDonationTrigger(string itemId)
    {
        MultiplayerUtils.broadcastTrigger(new MultiplayerUtils.TriggerPackage($"{CMF.Manifest.UniqueID}_MuseumDonation", itemId, itemId, "ArchaeologyHouse"));
    }
    
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(MuseumMenu), nameof(MuseumMenu.receiveLeftClick))]
    public static IEnumerable<CodeInstruction> MuseumMenu_receiveLeftClick_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
    {
        var code = instructions.ToList();
        try
        {
            var matcher = new CodeMatcher(code, il);

            matcher.MatchStartForward(
                new CodeMatch(op => op.IsLdloc()),
                new CodeMatch(OpCodes.Callvirt),
                new CodeMatch(OpCodes.Callvirt),
                new CodeMatch(OpCodes.Ldstr, "stoneStep")
            );

            var loc = matcher.Operand;

            matcher.MatchEndForward(
                new CodeMatch(OpCodes.Ldstr, "stoneStep")
            ).Advance(1);

            matcher.Insert(
                new CodeInstruction(OpCodes.Ldloc_S, loc),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Item), nameof(Item.QualifiedItemId))),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(VanillaMuseumPatches), nameof(vanillaDonationTrigger)))
            );

            return matcher.InstructionEnumeration();
        }
        catch (Exception ex)
        {
            Log.Error("Error in CMF.LibraryMuseumPatches_MuseumMenu_receiveLeftClick_Transpiler: \n" + ex);
            return code;
        }
    }
}