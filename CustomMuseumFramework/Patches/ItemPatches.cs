using HarmonyLib;
using StardewValley;

namespace CustomMuseumFramework.Patches;

[HarmonyPatch(typeof(Item))]
public static class ItemPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(Item.canStackWith))]
    public static bool canStackWith_Prefix(Item __instance, ref bool __result, ISalable other)
    {
        if (__instance.modData.ContainsKey("CMF_Position") || (other is Item item && item.modData.ContainsKey("CMF_Position")))
        {
            __result = false;
            return false;
        }

        return true;
    }
}