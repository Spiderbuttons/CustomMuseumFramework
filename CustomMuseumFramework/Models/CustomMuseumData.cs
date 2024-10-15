using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace CustomMuseumFramework.Models;

public class CustomMuseumData
{
    public string Id { get; set; } = "";
    public string? Owner { get; set; } = null;
    public Vector2 OwnerTile { get; set; } = new Vector2(-1f, -1f);
    public bool RequireOwnerForDonation { get; set; } = false;
    
    public List<string> ValidItemIds { get; set; } = [];
    public List<string> ValidContextTags { get; set; } = [];
    public List<int> ValidCategories { get; set; } = [];
    
    public List<CustomMuseumRewardData> Rewards { get; set; } = [];
    public List<int> Milestones { get; set; } = [];
    
    public string MessageOnDonation { get; set; } = $"{CMF.Manifest.UniqueID}_OnDonation";
    public string MessageOnMilestone { get; set; } = $"{CMF.Manifest.UniqueID}_OnMilestone";
    public string MessageOnCompletion { get; set; } = $"{CMF.Manifest.UniqueID}_OnCompletion";
}