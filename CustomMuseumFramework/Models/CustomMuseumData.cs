using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.ItemTypeDefinitions;

namespace CustomMuseumFramework.Models;

public class CustomMuseumData
{
    public string Id { get; set; } = "";
    public string Owner { get; set; } = "";
    
    public List<string> ValidItemIds { get; set; } = [];
    public List<string> ValidContextTags { get; set; } = [];
    public List<int> ValidCategories { get; set; } = [];
    
    public List<CustomMuseumRewardData> Rewards { get; set; } = [];
    public List<int> Milestones { get; set; } = [];
    
    public string MessageOnDonation { get; set; } = $"{CMF.Manifest.UniqueID}_OnDonation";
    public string MessageOnMilestone { get; set; } = $"{CMF.Manifest.UniqueID}_OnMilestone";
    public string MessageOnCompletion { get; set; } = $"{CMF.Manifest.UniqueID}_OnCompletion";
}