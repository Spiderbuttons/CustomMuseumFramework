using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using CustomMuseumFramework.Helpers;
using HarmonyLib;
using StardewValley;

namespace CustomMuseumFramework.Patches;

[HarmonyPatch]
[HarmonyPriority(Priority.Last)]
public static class DescriptionPatches
{
    static IEnumerable<MethodBase> TargetMethods()
    {
        // return every method that is overriden from StardewValley.Item:getDescription()
        foreach (var method in Assembly.GetAssembly(typeof(Item))?.GetTypes()
                     .Where(t => t.IsSubclassOf(typeof(Item)) && !t.IsSubclassOf(typeof(Tool)) && t != typeof(Tool))
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

    static void Postfix(Item __instance, ref string __result)
    {
        if (!CMF.GlobalDonatableItems.TryGetValue(__instance.QualifiedItemId, out var museumDict)) return;
        
        var museum = museumDict.FirstOrDefault(kvp => !kvp.Value).Key;
        if (museum == null || !museum.MuseumData.ShowDonationHint) return;
        
        var text = string.Format(museum.MuseumData.Strings.CanBeDonated ?? CMF.DefaultStrings.CanBeDonated!,
            museum.Museum.DisplayName, museum.MuseumData.Owner is not null ? Game1.getCharacterFromName(museum.MuseumData.Owner?.Name)?.displayName ?? "A museum owner" : "A museum owner");
        
        ///// Keeping this around in case I ever wanna display more than one.
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