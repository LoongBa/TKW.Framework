using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace TKW.Framework.Common.Tools;

/// <summary>
/// 批次ID扩展方法
/// </summary>
public static class BatchIdExtensions
{
    #region IEnumerable<T> Extensions

    /// <summary>
    /// 为集合中的每个元素生成批次ID并赋值（延迟执行，不排序）。
    /// 注意：未排序的数据可能导致生成的批次ID不连续！建议先对数据排序。
    /// </summary>
    public static IEnumerable<T> WithBatchIds<T>(this IEnumerable<T> source,
        Action<T, string> idSetter,
        int length = 32,
        string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(idSetter);

        // 调试模式下警告未排序数据
        DebugWarnIfUnsorted();

        foreach (var item in source)
        {
            // 修改点：调用静态方法
            var batchId = BatchIdGenerator.GenerateBatchId(length, prefix);
            idSetter(item, batchId);
            yield return item;
        }
    }

    /// <summary>
    /// 为集合中的每个元素生成批次ID并赋值（立即执行，不排序）。
    /// </summary>
    public static IReadOnlyList<T> ApplyBatchIds<T>(this IEnumerable<T> source,
        Action<T, string> idSetter,
        int length = 32,
        string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(idSetter);

        return [.. source.WithBatchIds(idSetter, length, prefix)];
    }

    /// <summary>
    /// 为集合中的每个元素生成批次ID并返回包含批次ID的新对象（非原地修改，不排序）。
    /// </summary>
    public static IEnumerable<TResult> MapWithBatchIds<T, TResult>(this IEnumerable<T> source,
        Func<T, string, TResult> resultSelector,
        int length = 32,
        string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(resultSelector);

        // 调试模式下警告未排序数据
        DebugWarnIfUnsorted();

        foreach (var item in source)
        {
            // 修改点：调用静态方法
            var batchId = BatchIdGenerator.GenerateBatchId(length, prefix);
            yield return resultSelector(item, batchId);
        }
    }
    #endregion

    #region IAsyncEnumerable<T> Extensions

    /// <summary>
    /// 异步为集合中的每个元素生成批次ID并赋值（不排序）。
    /// 注意：未排序的数据可能导致生成的批次ID不连续！建议先对数据排序。
    /// </summary>
    public static async IAsyncEnumerable<T> WithBatchIdsAsync<T>(this IAsyncEnumerable<T> source,
        Action<T, string> idSetter,
        int length = 32,
        string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(idSetter);

        // 调试模式下警告未排序数据
        DebugWarnIfUnsorted();

        await foreach (var item in source)
        {
            // 修改点：调用静态方法
            var batchId = BatchIdGenerator.GenerateBatchId(length, prefix);
            idSetter(item, batchId);
            yield return item;
        }
    }

    /// <summary>
    /// 异步为集合中的每个元素生成批次ID并赋值（立即执行，不排序）。
    /// </summary>
    public static async ValueTask<IReadOnlyList<T>> ApplyBatchIdsAsync<T>(this IAsyncEnumerable<T> source,
        Action<T, string> idSetter,
        int length = 32,
        string prefix = "")
    {
        if (source == null)
            return [];

        return await source.WithBatchIdsAsync(idSetter, length, prefix).ToListAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 异步为集合中的每个元素生成批次ID并返回包含批次ID的新对象（非原地修改，不排序）。
    /// </summary>
    public static async IAsyncEnumerable<TResult> MapWithBatchIdsAsync<T, TResult>(this IAsyncEnumerable<T> source,
        Func<T, string, TResult> resultSelector,
        int length = 32,
        string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(resultSelector);

        // 调试模式下警告未排序数据
        DebugWarnIfUnsorted();

        await foreach (var item in source)
        {
            // 修改点：调用静态方法
            var batchId = BatchIdGenerator.GenerateBatchId(length, prefix);
            yield return resultSelector(item, batchId);
        }
    }

    #endregion

    [Conditional("DEBUG")]
    private static void DebugWarnIfUnsorted()
    {
        Debug.WriteLine("警告：未排序的数据可能导致批次ID不连续！建议使用带排序的方法。");
    }
}
