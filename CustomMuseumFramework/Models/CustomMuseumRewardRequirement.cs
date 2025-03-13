using System.Collections.Generic;

namespace CustomMuseumFramework.Models;

public class CustomMuseumRewardRequirement
{
    public List<string>? ContextTags;
    public List<string>? ItemIds;
    public List<int>? Categories;

    public MatchType MatchType = MatchType.Any;
    public int Count;
}

public enum MatchType
{
    Any,
    All
}