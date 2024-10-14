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
    
    public string MessageOnDonate { get; set; } = "{0} donated '{1}' to the {2} museum.";
    public string MessageOnMilestone { get; set; } = "{0} Farm has donated {1} pieces to the {2} museum.";
    public string MessageOnCompletion { get; set; } = "{0} Farm has completed the {1} museum collection.";
}