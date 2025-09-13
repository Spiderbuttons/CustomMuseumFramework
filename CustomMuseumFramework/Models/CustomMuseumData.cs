using System.Collections.Generic;
using CustomMuseumFramework.Helpers;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;

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
    
    [JsonConverter(typeof(SingleOrListConverter<Rectangle>))]
    public List<Rectangle> Bounds { get; set; } = [new Rectangle(0, 0, 0, 0)];
    
    public List<DonationRequirement> DonationRequirements { get; set; } = [];
    
    public List<DonationRequirement> BlacklistedDonations { get; set; } = [];
    
    public List<DonationRequirement> WhitelistedDonations { get; set; } = [];
    
    public int? CompletionNumber { get; set; } = null;
    
    public bool CountInvalidDonations { get; set; } = false;

    public string? AllowRetrieval { get; set; } = "FALSE";
    
    public string? ShowDonationHint { get; set; }
    
    public string? OverrideDescription { get; set; } = "FALSE";

    public InteractionData PedestalAction { get; set; } = new InteractionData();
    
    public List<CustomMuseumReward> Rewards { get; set; } = [];
    
    public List<int> Milestones { get; set; } = [];
    
    public MuseumStrings Strings { get; set; } = new MuseumStrings();
}

public class OwnerData
{
    public string? Name { get; set; } = null;
    public Rectangle? Area { get; set; } = null;
    public string? RequiredForDonation { get; set; } = "FALSE";
}

public class MuseumStrings
{
    public string? OnDonation { get; set; } = "";
    public string? OnMilestone { get; set; } = "";
    public string? OnCompletion { get; set; } = "";
    
    public string? MenuDonate { get; set; }
    public string? MenuCollect { get; set; }
    public string? MenuRearrange { get; set; }
    public string? MenuRetrieve { get; set; }
    
    public string? ClockedOut { get; set; }
    
    public string? Busy_Owner { get; set; }
    public string? Busy_NoOwner { get; set; }
    
    public string? MuseumComplete_Owner { get; set; }
    public string? MuseumComplete_NoOwner { get; set; }
    
    public string? NothingToDonate_Owner { get; set; }
    public string? NothingToDonate_NoOwner { get; set; }
    
    public string? NoDonations_Owner { get; set; }
    public string? NoDonations_NoOwner { get; set; }
    
    public string? CanBeDonated { get; set; }

    public string? EmptyPlaque { get; set; } = "";
}