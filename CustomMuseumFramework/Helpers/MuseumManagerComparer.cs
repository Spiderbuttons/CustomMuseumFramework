using System;
using System.Collections.Generic;

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
        return x.MuseumData.OverrideDescription switch
        {
            true when !y.MuseumData.OverrideDescription => -1,
            false when y.MuseumData.OverrideDescription => 1,
            _ => String.Compare(x.MuseumData.Id, y.MuseumData.Id, StringComparison.Ordinal)
        };
    }
}