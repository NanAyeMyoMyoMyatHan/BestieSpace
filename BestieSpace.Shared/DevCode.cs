using System;
using System.Collections.Generic;

namespace BestieSpace.Shared;

public static class DevCode
{
    public static readonly int[] AllowedPageSizes = { 10, 20, 30, 50, 100 };
    public const int DefaultPageSize = 30;
    public const int MaximumPageSize = 100;

    public static int NormalizePageSize(this int requestedPageSize, int defaultSize = DefaultPageSize, int maxSize = MaximumPageSize)
    {
        if (requestedPageSize <= 0) return defaultSize;
        if (requestedPageSize > maxSize) return maxSize;
        return requestedPageSize;
    }
}
