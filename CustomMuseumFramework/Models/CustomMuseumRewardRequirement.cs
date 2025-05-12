using System.Collections.Generic;

namespace CustomMuseumFramework.Models;

public class CustomMuseumRewardRequirement
{
    public List<int>? Categories;
    public List<string>? ContextTags;
    public List<string>? ItemIds;

    public MatchType MatchType = MatchType.Any;
    public int Count;
}