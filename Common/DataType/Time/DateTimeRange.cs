using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace TKW.Framework.Common.DataType.Time;

/// <summary>
/// 日期区间，前闭后开：[Start, End)
/// <para>
/// <b>注意：</b>Start 和 End 必须同为 UTC 或同为本地时间，禁止混用。
/// </para>
/// </summary>
[Serializable]
[JsonConverter(typeof(DateTimeRangeJsonConverter))]
public readonly struct DateTimeRange : IEquatable<DateTimeRange>, IEnumerable<DateTime>, ISerializable, IComparable<DateTimeRange>
{
    public DateTime Start { get; }
    public DateTime End { get; }

    /// <summary>
    /// 空区间（Start == End == DateTime.MinValue）
    /// </summary>
    public static DateTimeRange Empty => new(DateTime.MinValue, DateTime.MinValue);

    public DateTimeRange(DateTime start, DateTime end)
    {
        if (start.Kind != end.Kind)
            throw new ArgumentException("Start and End must have the same DateTimeKind (UTC or Local).");
        if (start > end)
            throw new ArgumentOutOfRangeException(nameof(end), "Start must be less than or equal to End.");
        Start = start;
        End = end;
    }

    // 反序列化构造器
    private DateTimeRange(SerializationInfo info, StreamingContext context)
    {
        Start = info.GetDateTime(nameof(Start));
        End = info.GetDateTime(nameof(End));
    }

    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        info.AddValue(nameof(Start), Start);
        info.AddValue(nameof(End), End);
    }

    /// <summary>
    /// 通过 DateOnly 创建区间，区间为 [start, end+1)
    /// </summary>
    public static DateTimeRange FromDateOnly(DateOnly start, DateOnly end, DateTimeKind kind = DateTimeKind.Unspecified)
        => new(start.ToDateTime(TimeOnly.MinValue, kind), end.ToDateTime(TimeOnly.MinValue, kind).AddDays(1));

    /// <summary>
    /// 通过单个 DateOnly 创建区间，区间为 [date, date+1)
    /// </summary>
    public static DateTimeRange FromDateOnly(DateOnly date, DateTimeKind kind = DateTimeKind.Unspecified)
        => new(date.ToDateTime(TimeOnly.MinValue, kind), date.ToDateTime(TimeOnly.MinValue, kind).AddDays(1));

    /// <summary>
    /// 转为 DateOnly 区间（仅当区间为整天时有效，否则抛异常）
    /// </summary>
    public (DateOnly Start, DateOnly End) ToDateOnlyRange()
    {
        if (Start.TimeOfDay != System.TimeSpan.Zero || End.TimeOfDay != System.TimeSpan.Zero)
            throw new InvalidOperationException("Only full-day ranges can be converted to DateOnly.");
        return (DateOnly.FromDateTime(Start), DateOnly.FromDateTime(End.AddDays(-1)));
    }

    /// <summary>
    /// 是否为有效区间（Start &lt;= End）
    /// </summary>
    public bool IsValid => Start <= End;

    /// <summary>
    /// 是否为空区间（Start == End）
    /// </summary>
    public bool IsEmpty => Start == End;

    /// <summary>
    /// 是否有开始值（不为默认值）
    /// </summary>
    public bool HasStartDateTime => Start != DateTime.MaxValue && Start != DateTime.MinValue;

    /// <summary>
    /// 是否有结束值（不为默认值）
    /// </summary>
    public bool HasEndDateTime => End != DateTime.MaxValue && End != DateTime.MinValue;

    /// <summary>
    /// 区间间隔
    /// </summary>
    public TimeSpan TimeSpan() => End - Start;

    /// <summary>
    /// 指定的时间是否在范围内：前闭后开
    /// </summary>
    public bool IsInRange(DateTime dateTime)
    {
        if (dateTime.Kind != Start.Kind)
            throw new ArgumentException("DateTime.Kind must match the range's Kind.");
        if (dateTime == DateTime.MinValue || dateTime == DateTime.MaxValue) return false;
        return dateTime >= Start && dateTime < End;
    }

    /// <summary>
    /// 是否包含另一区间
    /// </summary>
    public bool Contains(DateTimeRange other)
        => Start <= other.Start && End >= other.End && Start.Kind == other.Start.Kind;

    /// <summary>
    /// 是否重叠
    /// </summary>
    public bool Overlaps(DateTimeRange other)
        => Start < other.End && End > other.Start && Start.Kind == other.Start.Kind;

    /// <summary>
    /// 是否相邻
    /// </summary>
    public bool IsAdjacentTo(DateTimeRange other)
        => (End == other.Start || Start == other.End) && Start.Kind == other.Start.Kind;

    /// <summary>
    /// 交集
    /// </summary>
    public DateTimeRange? Intersect(DateTimeRange other)
    {
        if (Start.Kind != other.Start.Kind) return null;
        var newStart = Start > other.Start ? Start : other.Start;
        var newEnd = End < other.End ? End : other.End;
        return newStart < newEnd ? new DateTimeRange(newStart, newEnd) : null;
    }

    /// <summary>
    /// 并集（仅当区间重叠或相邻时）
    /// </summary>
    public DateTimeRange? Union(DateTimeRange other)
    {
        if (Start.Kind != other.Start.Kind) return null;
        if (!Overlaps(other) && !IsAdjacentTo(other)) return null;
        var newStart = Start < other.Start ? Start : other.Start;
        var newEnd = End > other.End ? End : other.End;
        return new DateTimeRange(newStart, newEnd);
    }

    /// <summary>
    /// 区间拆分（按天/小时/分钟）
    /// </summary>
    public IEnumerable<DateTimeRange> Split(DateTimeStep step)
    {
        if (IsEmpty) yield break;
        var current = Start;
        while (current < End)
        {
            var next = step switch
            {
                DateTimeStep.Day => current.AddDays(1),
                DateTimeStep.Hour => current.AddHours(1),
                DateTimeStep.Minute => current.AddMinutes(1),
                _ => throw new NotSupportedException()
            };
            yield return new DateTimeRange(current, next < End ? next : End);
            current = next;
        }
    }

    /// <summary>
    /// 区间偏移
    /// </summary>
    public DateTimeRange Shift(TimeSpan offset) => new(Start + offset, End + offset);

    /// <summary>
    /// 区间缩放（向两端扩展/收缩）
    /// </summary>
    public DateTimeRange Expand(TimeSpan delta) => new(Start - delta, End + delta);

    /// <summary>
    /// 自定义格式化
    /// </summary>
    public string ToString(string format)
        => $"[{Start.ToString(format)} ~ {End.ToString(format)})";

    /// <summary>
    /// 重写 ToString
    /// </summary>
    public override string ToString() => ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>
    /// 解析字符串为区间，格式：[yyyy-MM-dd HH:mm:ss ~ yyyy-MM-dd HH:mm:ss)
    /// </summary>
    public static DateTimeRange Parse(string s, string format = "yyyy-MM-dd HH:mm:ss")
    {
        if (string.IsNullOrWhiteSpace(s)) throw new ArgumentNullException(nameof(s));
        var parts = s.Trim('[', ')').Split('~');
        if (parts.Length != 2) throw new FormatException("Invalid DateTimeRange format.");
        var start = DateTime.ParseExact(parts[0].Trim(), format, CultureInfo.InvariantCulture);
        var end = DateTime.ParseExact(parts[1].Trim(), format, CultureInfo.InvariantCulture);
        return new DateTimeRange(start, end);
    }

    /// <summary>
    /// 支持区间内的日期枚举（默认按天，可选按小时、分钟）
    /// </summary>
    public IEnumerable<DateTime> Enumerate(DateTimeStep step = DateTimeStep.Day)
    {
        if (IsEmpty) yield break;
        var current = Start;
        while (current < End)
        {
            yield return current;
            current = step switch
            {
                DateTimeStep.Day => current.AddDays(1),
                DateTimeStep.Hour => current.AddHours(1),
                DateTimeStep.Minute => current.AddMinutes(1),
                _ => throw new NotSupportedException()
            };
        }
    }

    /// <summary>
    /// 默认实现，按天枚举
    /// </summary>
    public IEnumerator<DateTime> GetEnumerator() => Enumerate().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// 重写 Equals
    /// </summary>
    public override bool Equals(object obj) => obj is DateTimeRange other && Equals(other);

    public bool Equals(DateTimeRange other) => Start == other.Start && End == other.End;

    public override int GetHashCode() => HashCode.Combine(Start, End);

    public static bool operator ==(DateTimeRange left, DateTimeRange right) => left.Equals(right);
    public static bool operator !=(DateTimeRange left, DateTimeRange right) => !(left == right);

    public int CompareTo(DateTimeRange other)
    {
        var cmp = Start.CompareTo(other.Start);
        return cmp != 0 ? cmp : End.CompareTo(other.End);
    }

    // 静态工厂方法举例
    public static DateTimeRange Today(DateTimeKind kind = DateTimeKind.Local)
    {
        var now = DateTime.Now;
        var today = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, kind);
        return new DateTimeRange(today, today.AddDays(1));
    }

    public static DateTimeRange ThisMonth(DateTimeKind kind = DateTimeKind.Local)
    {
        var now = DateTime.Now;
        var first = new DateTime(now.Year, now.Month, 1, 0, 0, 0, kind);
        return new DateTimeRange(first, first.AddMonths(1));
    }

    public static DateTimeRange ThisYear(DateTimeKind kind = DateTimeKind.Local)
    {
        var now = DateTime.Now;
        var first = new DateTime(now.Year, 1, 1, 0, 0, 0, kind);
        return new DateTimeRange(first, first.AddYears(1));
    }

    public static DateTimeRange LastNDays(int n, DateTimeKind kind = DateTimeKind.Local)
    {
        var now = DateTime.Now;
        var start = now.Date.AddDays(-n);
        var end = now.Date;
        return new DateTimeRange(start, end);
    }
}