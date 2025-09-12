using System;
using System.Collections.Generic;
using StardewValley;

namespace CustomMuseumFramework.Helpers;

public class MuseumManagerComparer : IComparer<MuseumManager>
{
    public int Compare(MuseumManager? x, MuseumManager? y)
    {
        switch (x)
        {
            case null when y == null:
                return 0;
            case null:
                return -1;
        }

        if (y == null) return 1;
        try
        {
            return GameStateQuery.CheckConditions(x.MuseumData.OverrideDescription, x.Museum) switch
            {
                true when !GameStateQuery.CheckConditions(y.MuseumData.OverrideDescription, y.Museum) => -1,
                false when GameStateQuery.CheckConditions(y.MuseumData.OverrideDescription, y.Museum) => 1,
                _ => String.Compare(x.MuseumData.Id, y.MuseumData.Id, StringComparison.Ordinal)
            };
        }
        catch (Exception)
        {
            return String.Compare(x.MuseumData.Id, y.MuseumData.Id, StringComparison.Ordinal);
        }
    }
}