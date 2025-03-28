using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;

namespace CustomMuseumFramework.Models;

public class CustomMuseumData
{
    public string Id { get; set; } = "";

    public OwnerData? Owner { get; set; } = null;
    
    public Rectangle Bounds { get; set; } = new Rectangle(0, 0, 0, 0);
    
    public DonationCriteria DonationCriteria { get; set; } = new DonationCriteria();

    public bool AllowRetrieval { get; set; } = false;
    
    public List<CustomMuseumRewardData> Rewards { get; set; } = [];
    
    public List<int> Milestones { get; set; } = [];
    
    public MuseumStrings Strings { get; set; } = new MuseumStrings();

    [JsonIgnore] // TODO: Store possible donatables.
    public HashSet<string> DonatableItems { get; set; } = new HashSet<string>();
}

public class OwnerData
{
    public string? Name { get; set; } = null;
    public Rectangle? Area { get; set; } = null;
    public bool RequiredForDonation { get; set; } = false;
}

public class DonationCriteria
{
    public List<string>? ItemIds { get; set; } = null;
    public List<string>? ContextTags { get; set; } = null;
    public List<int>? Categories { get; set; } = null;
}

public class MuseumStrings
{
    // TODO: These need i18n.
    public string? OnDonation { get; set; } = null;
    public string? OnMilestone { get; set; } = null;
    public string? OnCompletion { get; set; } = null;
    
    public string? MenuDonate { get; set; } = null;
    public string? MenuCollect { get; set; } = null;
    public string? MenuRearrange { get; set; } = null;
    public string? MenuRetrieve { get; set; } = null;
    
    public string? Busy_Owner { get; set; } = null;
    public string? Busy_NoOwner { get; set; } = null;
    
    public string? MuseumComplete_Owner { get; set; } = null;
    public string? MuseumComplete_NoOwner { get; set; } = null;
    
    public string? NothingToDonate_Owner { get; set; } = null;
    public string? NothingToDonate_NoOwner { get; set; } = null;
    
    public string? NoDonations_Owner { get; set; } = null;
    public string? NoDonations_NoOwner { get; set; } = null;
}