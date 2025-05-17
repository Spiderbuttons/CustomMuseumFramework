using System.Linq;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.Quests;

namespace CustomMuseumFramework;

public static class QuestExtensions
{
    public static bool OnMuseumDonation(this Quest quest, Item? item, bool probe = false)
    {
        if (!CMF.QuestData.TryGetValue(quest.id.Value, out var qData)) return false;
        
        if (qData.Requirements is null || !qData.Requirements.Any() ||
            (quest.modData.TryGetValue("CMF_Complete", out var complete) && complete.EqualsIgnoreCase("true")))
        {
            if (!probe) quest.questComplete();
            return true;
        }

        bool changed = false;
        int completedRequirements = 0;
        foreach (var req in qData.Requirements)
        {
            if (!quest.modData.TryGetValue("CMF_Requirement_" + req.Id, out var value) || !int.TryParse(value, out var count)) continue;

            if (MuseumManager.DoesDonationSatisfyRequirement(item, req))
            {
                if (!probe)
                {
                    quest.modData["CMF_Requirement_" + req.Id] = (--count).ToString();
                }
                changed = true;
            }
            
            if (count <= 0) completedRequirements++;
        }
        
        if (completedRequirements >= qData.Requirements.Count)
        {
            if (!probe) quest.questComplete();
            return true;
        }
        
        return changed;
    }

    public static bool OnMuseumRetrieval(this Quest quest, Item? item)
    {
        // When a player removes an item from the museum, we need to increment the count for the requirement now so they can't exploit it by donating the same item over and over again.
        if (!CMF.QuestData.TryGetValue(quest.id.Value, out var qData)) return false;
        
        if (qData.Requirements is null || !qData.Requirements.Any() ||
            (quest.modData.TryGetValue("CMF_Complete", out var complete) && complete.EqualsIgnoreCase("true")))
        {
            // We can ignore it if the quest is already marked as complete though (or has no requirements obviously).
            return false;
        }

        bool changed = false;
        foreach (var req in qData.Requirements)
        {
            if (!quest.modData.TryGetValue("CMF_Requirement_" + req.Id, out var value) || !int.TryParse(value, out var count)) continue;

            if (MuseumManager.DoesDonationSatisfyRequirement(item, req))
            {
                count++;
                quest.modData["CMF_Requirement_" + req.Id] = count.ToString();
                changed = true;
            }
        }

        return changed;
    }
}