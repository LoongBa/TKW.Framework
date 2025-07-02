using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TKW.Framework.Common.Tools.BatchId;

/// <summary>
/// 批次ID扩展方法
/// </summary>
public static class BatchIdExtensions
{
    /// <summary>
    /// 为集合中的每个元素生成批次ID并赋值（延迟执行，不排序）。
    /// 注意：未排序的数据可能导致生成的批次ID不连续！建议先对数据排序。
    /// </summary>
    public static IEnumerable<T> WithBatchIds<T>(
        this IEnumerable<T> source,
        Func<T, DateTime> timestampSelector,
        Action<T, string> idSetter,
        string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(timestampSelector);
        ArgumentNullException.ThrowIfNull(idSetter);

        var generator = new BatchIdGenerator(prefix);

        // 调试模式下警告未排序数据
        DebugWarnIfUnsorted();

        foreach (var item in source)
        {
            var timestamp = timestampSelector(item);
            var batchId = generator.GenerateBatchId(timestamp);
            idSetter(item, batchId);
            yield return item;
        }
    }

    /// <summary>
    /// 为集合中的每个元素生成批次ID并赋值（立即执行，带排序，优化为单次枚举）。
    /// </summary>
    public static IReadOnlyList<T> ApplySortedBatchIds<T>(
        this IEnumerable<T> source,
        Func<T, DateTime> timestampSelector,
        Action<T, string> idSetter,
        Func<T, object> sortSelector = null,
        string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(timestampSelector);
        ArgumentNullException.ThrowIfNull(idSetter);

        // 优化：将排序和批次ID生成合并为单次枚举
        var generator = new BatchIdGenerator(prefix);

        var sortedData = sortSelector != null
            ? [.. source.OrderBy(sortSelector)]
            : source.OrderBy(timestampSelector).ToList();

        foreach (var item in sortedData)
        {
            var timestamp = timestampSelector(item);
            var batchId = generator.GenerateBatchId(timestamp);
            idSetter(item, batchId);
        }

        return sortedData;
    }

    /// <summary>
    /// 为集合中的每个元素生成批次ID并赋值（立即执行，不排序）。
    /// </summary>
    public static IReadOnlyList<T> ApplyBatchIds<T>(
        this IEnumerable<T> source,
        Func<T, DateTime> timestampSelector,
        Action<T, string> idSetter,
        string prefix = "")
    {
        return [.. source.WithBatchIds(timestampSelector, idSetter, prefix)];
    }

    /// <summary>
    /// 异步为集合中的每个元素生成批次ID并赋值（不排序）。
    /// 注意：未排序的数据可能导致生成的批次ID不连续！建议先对数据排序。
    /// </summary>
    public static async IAsyncEnumerable<T> WithBatchIdsAsync<T>(
        this IAsyncEnumerable<T> source,
        Func<T, DateTime> timestampSelector,
        Action<T, string> idSetter,
        string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(timestampSelector);
        ArgumentNullException.ThrowIfNull(idSetter);

        var generator = new BatchIdGenerator(prefix);

        // 调试模式下警告未排序数据
        DebugWarnIfUnsorted();

        await foreach (var item in source)
        {
            var timestamp = timestampSelector(item);
            var batchId = generator.GenerateBatchId(timestamp);
            idSetter(item, batchId);
            yield return item;
        }
    }

    /// <summary>
    /// 异步为集合中的每个元素生成批次ID并赋值（带排序）。
    /// 注意：此方法会将数据全量加载到内存进行排序，大数据集需谨慎使用。
    /// </summary>
    public static async IAsyncEnumerable<T> WithSortedBatchIdsAsync<T>(
        this IAsyncEnumerable<T> source,
        Func<T, DateTime> timestampSelector,
        Action<T, string> idSetter,
        Func<T, object> sortSelector = null,
        string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(timestampSelector);
        ArgumentNullException.ThrowIfNull(idSetter);

        // 收集所有元素到内存排序
        var list = await source.ToListAsync();
        var sortedData = sortSelector != null
            ? list.OrderBy(sortSelector)
            : list.OrderBy(timestampSelector);

        var generator = new BatchIdGenerator(prefix);

        foreach (var item in sortedData)
        {
            var timestamp = timestampSelector(item);
            var batchId = generator.GenerateBatchId(timestamp);
            idSetter(item, batchId);
            yield return item;
        }
    }

    /// <summary>
    /// 异步为集合中的每个元素生成批次ID并赋值（立即执行，不排序）。
    /// </summary>
    public static async ValueTask<IReadOnlyList<T>> ApplyBatchIdsAsync<T>(
        this IAsyncEnumerable<T> source,
        Func<T, DateTime> timestampSelector,
        Action<T, string> idSetter,
        string prefix = "")
    {
        if (source == null)
            return [];

        return await source.WithBatchIdsAsync(timestampSelector, idSetter, prefix).ToListAsync();
    }

    /// <summary>
    /// 异步为集合中的每个元素生成批次ID并赋值（立即执行，带排序）。
    /// 注意：此方法会将数据全量加载到内存进行排序，大数据集需谨慎使用。
    /// </summary>
    public static async ValueTask<IReadOnlyList<T>> ApplySortedBatchIdsAsync<T>(
        this IAsyncEnumerable<T> source,
        Func<T, DateTime> timestampSelector,
        Action<T, string> idSetter,
        Func<T, object> sortSelector = null,
        string prefix = "")
    {
        if (source == null)
            return [];

        // 优化：合并排序和批次ID生成
        var list = await source.ToListAsync();
        var sortedData = sortSelector != null
            ? [.. list.OrderBy(sortSelector)]
            : list.OrderBy(timestampSelector).ToList();

        var generator = new BatchIdGenerator(prefix);
        foreach (var item in sortedData)
        {
            var timestamp = timestampSelector(item);
            var batchId = generator.GenerateBatchId(timestamp);
            idSetter(item, batchId);
        }

        return sortedData;
    }

    /// <summary>
    /// 为集合中的每个元素生成批次ID并返回包含批次ID的新对象（非原地修改，不排序）。
    /// </summary>
    public static IEnumerable<TResult> MapWithBatchIds<T, TResult>(
        this IEnumerable<T> source,
        Func<T, DateTime> timestampSelector,
        Func<T, string, TResult> resultSelector,
        string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(timestampSelector);
        ArgumentNullException.ThrowIfNull(resultSelector);

        var generator = new BatchIdGenerator(prefix);

        // 调试模式下警告未排序数据
        DebugWarnIfUnsorted();

        foreach (var item in source)
        {
            var timestamp = timestampSelector(item);
            var batchId = generator.GenerateBatchId(timestamp);
            yield return resultSelector(item, batchId);
        }
    }

    /// <summary>
    /// 为集合中的每个元素生成批次ID并返回包含批次ID的新对象（非原地修改，带排序）。
    /// </summary>
    public static IEnumerable<TResult> MapWithSortedBatchIds<T, TResult>(
        this IEnumerable<T> source,
        Func<T, DateTime> timestampSelector,
        Func<T, string, TResult> resultSelector,
        Func<T, object> sortSelector = null,
        string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(timestampSelector);
        ArgumentNullException.ThrowIfNull(resultSelector);

        var sortedData = sortSelector != null
            ? [.. source.OrderBy(sortSelector)]
            : source.OrderBy(timestampSelector).ToList();

        var generator = new BatchIdGenerator(prefix);

        foreach (var item in sortedData)
        {
            var timestamp = timestampSelector(item);
            var batchId = generator.GenerateBatchId(timestamp);
            yield return resultSelector(item, batchId);
        }
    }

    /// <summary>
    /// 异步为集合中的每个元素生成批次ID并返回包含批次ID的新对象（非原地修改，不排序）。
    /// </summary>
    public static async IAsyncEnumerable<TResult> MapWithBatchIdsAsync<T, TResult>(
        this IAsyncEnumerable<T> source,
        Func<T, DateTime> timestampSelector,
        Func<T, string, TResult> resultSelector,
        string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(timestampSelector);
        ArgumentNullException.ThrowIfNull(resultSelector);

        var generator = new BatchIdGenerator(prefix);

        // 调试模式下警告未排序数据
        DebugWarnIfUnsorted();

        await foreach (var item in source)
        {
            var timestamp = timestampSelector(item);
            var batchId = generator.GenerateBatchId(timestamp);
            yield return resultSelector(item, batchId);
        }
    }

    /// <summary>
    /// 异步为集合中的每个元素生成批次ID并返回包含批次ID的新对象（非原地修改，带排序）。
    /// 注意：此方法会将数据全量加载到内存进行排序，大数据集需谨慎使用。
    /// </summary>
    public static async IAsyncEnumerable<TResult> MapWithSortedBatchIdsAsync<T, TResult>(
        this IAsyncEnumerable<T> source,
        Func<T, DateTime> timestampSelector,
        Func<T, string, TResult> resultSelector,
        Func<T, object> sortSelector = null,
        string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(timestampSelector);
        ArgumentNullException.ThrowIfNull(resultSelector);

        // 收集所有元素到内存排序
        var list = await source.ToListAsync();
        var sortedData = sortSelector != null
            ? list.OrderBy(sortSelector)
            : list.OrderBy(timestampSelector);

        var generator = new BatchIdGenerator(prefix);

        foreach (var item in sortedData)
        {
            var timestamp = timestampSelector(item);
            var batchId = generator.GenerateBatchId(timestamp);
            yield return resultSelector(item, batchId);
        }
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private static void DebugWarnIfUnsorted()
    {
        System.Diagnostics.Debug.WriteLine("警告：未排序的数据可能导致批次ID不连续！建议使用带排序的方法。");
    }
}