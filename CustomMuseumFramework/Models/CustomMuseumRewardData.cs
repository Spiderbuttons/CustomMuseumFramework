using System.Collections.Generic;
using StardewValley.GameData;

namespace CustomMuseumFramework.Models;

public class CustomMuseumRewardData
{
    public string? Id;
    public List<DonationRequirementWithCount>? Requirements = null;
    public List<GenericSpawnItemDataWithCondition>? RewardItems = null;
    public List<string>? Actions = null;
    public bool RewardIsSpecial = false;
    public bool FlagOnCompletion = true;
}