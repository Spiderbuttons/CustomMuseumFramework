using System.Collections.Generic;
using StardewValley.GameData;

namespace CustomMuseumFramework.Models;

public class CustomMuseumReward
{
    public string? Id;
    public List<DonationRequirementWithCount>? Requirements = null;
    public List<GenericSpawnItemDataWithCondition>? RewardItems = null;
    public string? Action = null;
    public List<string>? Actions = null;
    public bool RewardIsSpecial = false;
    public bool FlagOnCompletion = true;
}