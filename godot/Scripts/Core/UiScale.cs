using System;

namespace ShotV.Core;

public static class UiScale
{
    public const int DefaultFontDelta = -1;
    public const int MinimumFontSize = 8;

    public static int Font(int baseSize, int delta = DefaultFontDelta)
    {
        return Math.Max(MinimumFontSize, baseSize + delta);
    }
}
