using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CustomMuseumFramework.Helpers;
using HarmonyLib;
using StardewValley;

using StardewValley.Objects;


namespace CustomMuseumFramework.Patches;

[HarmonyPatch]
[HarmonyPriority(Priority.Last)]
public static class DescriptionPatches
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        // return every method that is overriden from StardewValley.Item:getDescription()
        foreach (var method in Assembly.GetAssembly(typeof(Item))?.GetTypes()
                     .Where(t => t.IsSubclassOf(typeof(Item)) && !t.IsSubclassOf(typeof(Tool)) && t != typeof(Tool)) // Except for tools because they're dumb.
                     .Where(t => t
                         .GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic |
                                     BindingFlags.Instance)
                         .Any(m => m.Name == "getDescription" && !m.GetParameters().Any()))
                     .Select(t => t.GetMethod("getDescription"))!)
        {
            yield return method!;
        }

        yield return AccessTools.PropertyGetter(typeof(Tool), nameof(Tool.description));
    }

    public static void Postfix(Item __instance, ref string __result)
    {
        if (!CMF.GlobalDonatableItems.TryGetValue(__instance.QualifiedItemId, out var museumDict)) return;
        
        var museums = museumDict.Where(kvp => !kvp.Value)
            .Select(kvp => kvp.Key)
            .Where(m => m.MuseumData.ShowDonationHint)
            .ToList();
        
        if (!museums.Any()) return;
        var museum = museums.First();
        
        var text = museum.CAN_BE_DONATED();
        
        ///// Keeping this around in case I ever want to display more than one.
        // var text = museumDict
        //     .Where(kvp => !kvp.Value)
        //     .Select(kvp => $"{kvp.Key.Museum.DisplayName} would be interested in this.")
        //     .ToList()
        
        int width = __instance.GetType()
            .GetMethod("getDescriptionWidth", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(__instance, []) as int? ?? 272;
        
        if (museum.MuseumData.OverrideDescription) __result = Game1.parseText(text, Game1.smallFont, width);
        else __result += "\n\n" + Game1.parseText(text, Game1.smallFont, width);
    }
}

// Rings are also dumb and need special handling.
[HarmonyPatch(typeof(Ring))]
public static class RingPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(Ring.drawTooltip))]
    public static void Ring_drawTooltip_Prefix(Ring __instance, ref string __state, ref int y)
    {
        if (!CMF.GlobalDonatableItems.TryGetValue(__instance.QualifiedItemId, out var museumDict) ||
            __instance.description is null) return;

        __state = __instance.description;
        var museum = museumDict.FirstOrDefault(kvp => !kvp.Value).Key;
        if (museum == null || !museum.MuseumData.ShowDonationHint) return;

        var text = museum.CAN_BE_DONATED();

        if (museum.MuseumData.OverrideDescription) __instance.description = text;
        else __instance.description += "\n\n" + text;
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(Ring.drawTooltip))]
    public static void Ring_drawTooltip_Postfix(Ring __instance, string __state)
    {
        if (!CMF.GlobalDonatableItems.TryGetValue(__instance.QualifiedItemId, out _)) return;

        __instance.description = __state;
    }
}