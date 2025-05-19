using System.Collections.Generic;

namespace CustomMuseumFramework.Models;

public class CustomMuseumQuestData
{
    public string Id { get; set; } = "";
    public string MuseumId { get; set; } = "";
    public string? Title { get; set; } = null;
    public string? Description { get; set; } = null;
    public string? Hint { get; set; } = null;
    public List<DonationRequirementWithCount>? Requirements { get; set; } = null;
    public List<CustomMuseumNextQuest>? NextQuests { get; set; } = null;
    public int Reward { get; set; } = 0;
    public bool CanBeCancelled { get; set; } = true;
    public int TimeToComplete { get; set; } = -1;
    public string? ActionOnCompletion { get; set; } = null;
    public List<string>? ActionsOnCompletion { get; set; } = null;
}

public class CustomMuseumNextQuest
{
    public string Id { get; set; } = "";
    public bool HostOnly { get; set; } = false;
    public string? Condition { get; set; } = null;
}