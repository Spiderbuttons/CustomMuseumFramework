﻿using System.Linq;
using CustomMuseumFramework.Helpers;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Quests;
using StardewValley.TokenizableStrings;
using StardewValley.Triggers;

namespace CustomMuseumFramework.Patches;

[HarmonyPatch(typeof(Quest))]
public static class QuestPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(Quest.getQuestFromId))]
    public static bool getQuestFromId_Prefix(string? id, ref Quest __result)
    {
        if (id is null || !CMF.QuestData.TryGetValue(id, out var data)) return true;
        
        Quest quest = new Quest();
        quest.questType.Value = 1;
        quest.id.Value = id;
        quest.questTitle = TokenParser.ParseText(data.Title ?? "");
        quest.questDescription = TokenParser.ParseText(data.Description ?? "");
        quest.currentObjective = TokenParser.ParseText(data.Hint ?? "");
        quest.daysLeft.Value = data.TimeToComplete;

        if (data.NextQuests is not null)
        {
            foreach (var q in data.NextQuests.Where(q => Context.IsMainPlayer || !q.HostOnly))
            {
                quest.nextQuests.Add(q.Id);
            }
        }

        quest.showNew.Value = true;
        quest.moneyReward.Value = data.Reward;
        quest.rewardDescription.Value = null;
        quest.canBeCancelled.Value = data.CanBeCancelled;

        if (data.Requirements != null && data.Requirements.Any())
        {
            foreach (var req in data.Requirements.Where(req => req.Count > 0))
            {
                quest.modData["CMF_Requirement_" + req.Id] = req.Count.ToString();
            }
            quest.modData["CMF_Complete"] = "false";
        }
        else quest.modData["CMF_Complete"] = "true";
        
        quest.modData["CMF_MuseumId"] = data.MuseumId;

        __result = quest;
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Farmer), nameof(Farmer.addQuest))]
    public static void addQuest_Postfix(Farmer __instance, string? questId)
    {
        if (questId is null || !CMF.QuestData.TryGetValue(questId, out var data)) return;

        foreach (var quest in __instance.questLog)
        {
            if (!quest.id.Value.Equals(data.Id)) continue;

            if (data.Requirements is null || !data.Requirements.Any())
            {
                quest.questComplete();
                return;
            }

            if (!quest.modData.TryGetValue("CMF_MuseumId", out var museumId) || !CMF.MuseumManagers.TryGetValue(museumId, out var museum)) return;
            
            foreach (var req in data.Requirements)
            {
                if (!quest.modData.TryGetValue("CMF_Requirement_" + req.Id, out var value) || !int.TryParse(value, out var count)) continue;
                
                count -= museum.DonationsSatisfyingRequirement(req);
                quest.modData["CMF_Requirement_" + req.Id] = count.ToString();
            }

            quest.OnMuseumDonation(null);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(Quest.questComplete))]
    public static void questComplete_Prefix(Quest __instance)
    {
        if (__instance.id?.Value is null || !CMF.QuestData.TryGetValue(__instance.id.Value, out var data)) return;

        if (data.ActionOnCompletion is not null)
        {
            if (!TriggerActionManager.TryRunAction(data.ActionOnCompletion, out string error, out _))
            {
                Log.Error(error);
            }
        }

        if (data.ActionsOnCompletion is not null && data.ActionsOnCompletion.Any())
        {
            foreach (var action in data.ActionsOnCompletion)
            {
                if (!TriggerActionManager.TryRunAction(action, out string error, out _))
                {
                    Log.Error(error);
                }
            }
        }

        if (!__instance.nextQuests.Any()) return;

        foreach (var q in __instance.nextQuests)
        {
            var dataQuest = data.NextQuests?.Find(x => x.Id.Equals(q));
            if (dataQuest is not null && !GameStateQuery.CheckConditions(dataQuest.Condition))
            {
                __instance.nextQuests.Remove(q);
            }
        }
    }
}