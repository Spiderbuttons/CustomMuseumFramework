using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace CustomMuseumFramework.Models;

public enum MatchType
{
    Any,
    All
}

public class CustomMuseumData
{
    public string Id { get; set; } = "";

    public OwnerData? Owner { get; set; } = null;
    
    public Rectangle Bounds { get; set; } = new Rectangle(0, 0, 0, 0);
    
    public List<DonationRequirement> DonationRequirements { get; set; } = [];

    public bool AllowRetrieval { get; set; } = false;
    
    public bool ShowDonationHint { get; set; } = true;
    
    public bool OverrideDescription { get; set; } = false;
    
    public List<CustomMuseumRewardData> Rewards { get; set; } = [];
    
    public List<int> Milestones { get; set; } = [];
    
    public MuseumStrings Strings { get; set; } = new MuseumStrings();
}

public class OwnerData
{
    public string? Name { get; set; } = null;
    public Rectangle? Area { get; set; } = null;
    public bool RequiredForDonation { get; set; } = false;
}

public class DonationRequirement
{
    public string Id { get; set; } = "";
    public List<int>? Categories { get; set; } = null;
    public List<string>? ContextTags { get; set; } = null;
    public List<string>? ItemIds { get; set; } = null;
    
    public MatchType MatchType { get; set; } = MatchType.Any;
}

public class MuseumStrings
{
    public string? OnDonation { get; set; }
    public string? OnMilestone { get; set; }
    public string? OnCompletion { get; set; }
    
    public string? MenuDonate { get; set; }
    public string? MenuCollect { get; set; }
    public string? MenuRearrange { get; set; }
    public string? MenuRetrieve { get; set; }
    
    public string? Busy_Owner { get; set; }
    public string? Busy_NoOwner { get; set; }
    
    public string? MuseumComplete_Owner { get; set; }
    public string? MuseumComplete_NoOwner { get; set; }
    
    public string? NothingToDonate_Owner { get; set; }
    public string? NothingToDonate_NoOwner { get; set; }
    
    public string? NoDonations_Owner { get; set; }
    public string? NoDonations_NoOwner { get; set; }
    
    public string? CanBeDonated { get; set; }
}