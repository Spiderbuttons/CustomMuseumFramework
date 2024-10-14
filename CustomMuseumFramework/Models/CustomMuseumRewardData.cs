using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.GameData;
using StardewValley.ItemTypeDefinitions;

namespace CustomMuseumFramework.Models;

public class CustomMuseumRewardData : GenericSpawnItemData
{
    public string Id;
    public List<CustomMuseumRewardRequirement> Requirements;
    public List<GenericSpawnItemDataWithCondition> RewardItems;
    public bool RewardIsSpecial;
    public bool RewardIsRecipe;
    public List<string> Actions;
    public bool FlagOnCompletion;
}