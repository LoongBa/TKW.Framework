using System.Collections.Generic;

namespace TKW.Framework.Common.DataType;

/// <summary>
/// DateTimeRange/DateOnlyRange 批量区间操作扩展
/// </summary>
public static class RangeLinqExtensions
{
    public static IEnumerable<DateTimeRange> IntersectAll(this IEnumerable<DateTimeRange> ranges)
    {
        using var enumerator = ranges.GetEnumerator();
        if (!enumerator.MoveNext()) yield break;
        var result = enumerator.Current;
        while (enumerator.MoveNext())
        {
            var next = enumerator.Current;
            var intersect = result.Intersect(next);
            if (intersect == null)
            {
                yield break;
            }
            result = intersect.Value;
        }
        yield return result;
    }

    public static IEnumerable<DateTimeRange> UnionAll(this IEnumerable<DateTimeRange> ranges)
    {
        var sorted = new List<DateTimeRange>(ranges);
        sorted.Sort();
        if (sorted.Count == 0) yield break;
        var current = sorted[0];
        for (int i = 1; i < sorted.Count; i++)
        {
            var union = current.Union(sorted[i]);
            if (union != null)
                current = union.Value;
            else
            {
                yield return current;
                current = sorted[i];
            }
        }
        yield return current;
    }

    public static IEnumerable<DateOnlyRange> IntersectAll(this IEnumerable<DateOnlyRange> ranges)
    {
        using var enumerator = ranges.GetEnumerator();
        if (!enumerator.MoveNext()) yield break;
        var result = enumerator.Current;
        while (enumerator.MoveNext())
        {
            var next = enumerator.Current;
            var intersect = result.Intersect(next);
            if (intersect == null)
            {
                yield break;
            }
            result = intersect.Value;
        }
        yield return result;
    }

    public static IEnumerable<DateOnlyRange> UnionAll(this IEnumerable<DateOnlyRange> ranges)
    {
        var sorted = new List<DateOnlyRange>(ranges);
        sorted.Sort();
        if (sorted.Count == 0) yield break;
        var current = sorted[0];
        for (int i = 1; i < sorted.Count; i++)
        {
            var union = current.Union(sorted[i]);
            if (union != null)
                current = union.Value;
            else
            {
                yield return current;
                current = sorted[i];
            }
        }
        yield return current;
    }
}