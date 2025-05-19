using System.Collections.Generic;

namespace CustomMuseumFramework.Models;

public class DonationRequirement
{
    public string Id { get; set; } = "";
    public List<int>? Categories { get; set; } = null;
    public List<string>? ContextTags { get; set; } = null;
    public List<string>? ItemIds { get; set; } = null;
    
    public MatchType MatchType { get; set; } = MatchType.Any;
}


public class DonationRequirementWithCount : DonationRequirement
{
    public int Count { get; set; } = 1;
}