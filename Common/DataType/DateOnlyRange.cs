using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace TKW.Framework.Common.DataType;

/// <summary>
/// 仅日期区间，前闭后开：[Start, End)
/// </summary>
[Serializable]
[JsonConverter(typeof(DateOnlyRangeJsonConverter))]
public readonly struct DateOnlyRange : IEquatable<DateOnlyRange>, IEnumerable<DateOnly>, ISerializable, IComparable<DateOnlyRange>
{
    public DateOnly Start { get; }
    public DateOnly End { get; }

    public static DateOnlyRange Empty => new(DateOnly.MinValue, DateOnly.MinValue);

    public DateOnlyRange(DateOnly start, DateOnly end)
    {
        if (start > end)
            throw new ArgumentOutOfRangeException(nameof(end), "Start must be less than or equal to End.");
        Start = start;
        End = end;
    }

    private DateOnlyRange(SerializationInfo info, StreamingContext context)
    {
        Start = DateOnly.FromDateTime(info.GetDateTime(nameof(Start)));
        End = DateOnly.FromDateTime(info.GetDateTime(nameof(End)));
    }

    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        info.AddValue(nameof(Start), Start.ToDateTime(TimeOnly.MinValue));
        info.AddValue(nameof(End), End.ToDateTime(TimeOnly.MinValue));
    }

    public bool IsValid => Start <= End;
    public bool IsEmpty => Start == End;
    public int Length => End.DayNumber - Start.DayNumber;

    public bool IsInRange(DateOnly date) => date >= Start && date < End;
    public bool Contains(DateOnlyRange other) => Start <= other.Start && End >= other.End;
    public bool Overlaps(DateOnlyRange other) => Start < other.End && End > other.Start;
    public bool IsAdjacentTo(DateOnlyRange other) => End == other.Start || Start == other.End;

    public DateOnlyRange? Intersect(DateOnlyRange other)
    {
        var newStart = Start > other.Start ? Start : other.Start;
        var newEnd = End < other.End ? End : other.End;
        return newStart < newEnd ? new DateOnlyRange(newStart, newEnd) : null;
    }

    public DateOnlyRange? Union(DateOnlyRange other)
    {
        if (!Overlaps(other) && !IsAdjacentTo(other)) return null;
        var newStart = Start < other.Start ? Start : other.Start;
        var newEnd = End > other.End ? End : other.End;
        return new DateOnlyRange(newStart, newEnd);
    }

    public IEnumerable<DateOnlyRange> Split(int days = 1)
    {
        if (IsEmpty || days <= 0) yield break;
        var current = Start;
        while (current < End)
        {
            var next = current.AddDays(days);
            yield return new DateOnlyRange(current, next < End ? next : End);
            current = next;
        }
    }

    public DateOnlyRange Shift(int days) => new(Start.AddDays(days), End.AddDays(days));
    public DateOnlyRange Expand(int days) => new(Start.AddDays(-days), End.AddDays(days));

    public string ToString(string format) => $"[{Start.ToString(format)} ~ {End.ToString(format)})";
    public override string ToString() => ToString("yyyy-MM-dd");

    public static DateOnlyRange Parse(string s, string format = "yyyy-MM-dd")
    {
        if (string.IsNullOrWhiteSpace(s)) throw new ArgumentNullException(nameof(s));
        var parts = s.Trim('[', ')').Split('~');
        if (parts.Length != 2) throw new FormatException("Invalid DateOnlyRange format.");
        var start = DateOnly.ParseExact(parts[0].Trim(), format, CultureInfo.InvariantCulture);
        var end = DateOnly.ParseExact(parts[1].Trim(), format, CultureInfo.InvariantCulture);
        return new DateOnlyRange(start, end);
    }

    public IEnumerable<DateOnly> Enumerate(int stepDays = 1)
    {
        if (IsEmpty || stepDays <= 0) yield break;
        var current = Start;
        while (current < End)
        {
            yield return current;
            current = current.AddDays(stepDays);
        }
    }

    public IEnumerator<DateOnly> GetEnumerator() => Enumerate().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override bool Equals(object? obj) => obj is DateOnlyRange other && Equals(other);
    public bool Equals(DateOnlyRange other) => Start == other.Start && End == other.End;
    public override int GetHashCode() => HashCode.Combine(Start, End);
    public static bool operator ==(DateOnlyRange left, DateOnlyRange right) => left.Equals(right);
    public static bool operator !=(DateOnlyRange left, DateOnlyRange right) => !(left == right);

    public int CompareTo(DateOnlyRange other)
    {
        var cmp = Start.CompareTo(other.Start);
        return cmp != 0 ? cmp : End.CompareTo(other.End);
    }

    // 常用静态工厂方法
    public static DateOnlyRange Today()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        return new DateOnlyRange(today, today.AddDays(1));
    }

    public static DateOnlyRange ThisMonth()
    {
        var now = DateOnly.FromDateTime(DateTime.Now);
        var first = new DateOnly(now.Year, now.Month, 1);
        return new DateOnlyRange(first, first.AddMonths(1));
    }

    public static DateOnlyRange ThisYear()
    {
        var now = DateOnly.FromDateTime(DateTime.Now);
        var first = new DateOnly(now.Year, 1, 1);
        return new DateOnlyRange(first, first.AddYears(1));
    }

    public static DateOnlyRange LastNDays(int n)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        return new DateOnlyRange(today.AddDays(-n), today);
    }
}