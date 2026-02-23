using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace TKW.Framework.Common.Tools;

/// <summary>
/// ID 生成器扩展方法：包含业务便捷方法与集合批量生成
/// </summary>
public static class IdGeneratorExtensions
{
    #region 2. IEnumerable<T> 批量处理 (同步)

    extension<T>(IEnumerable<T> source)
    {
        /// <summary>
        /// 为集合中的每个元素生成 ID 并赋值（延迟执行）。
        /// </summary>
        public IEnumerable<T> WithIds(IIdGenerator generator,
            Action<T, string> idSetter,
            int length = 32,
            string prefix = null)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(generator);
            ArgumentNullException.ThrowIfNull(idSetter);

            DebugWarnIfUnsorted();

            foreach (var item in source)
            {
                var id = generator.NewId(length, prefix);
                idSetter(item, id);
                yield return item;
            }
        }

        /// <summary>
        /// 为集合中的每个元素生成 ID 并赋值（立即执行）。
        /// </summary>
        public IReadOnlyList<T> ApplyIds(IIdGenerator generator,
            Action<T, string> idSetter,
            int length = 32,
            string prefix = null)
        {
            return [.. source.WithIds(generator, idSetter, length, prefix)];
        }
    }

    #endregion

    #region 3. IAsyncEnumerable<T> 批量处理 (异步)

    extension<T>(IAsyncEnumerable<T> source)
    {
        /// <summary>
        /// 异步为集合中的每个元素生成 ID 并赋值。
        /// </summary>
        public async IAsyncEnumerable<T> WithIdsAsync(IIdGenerator generator,
            Action<T, string> idSetter,
            int length = 32,
            string prefix = null)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(generator);
            ArgumentNullException.ThrowIfNull(idSetter);

            DebugWarnIfUnsorted();

            await foreach (var item in source)
            {
                var id = generator.NewId(length, prefix);
                idSetter(item, id);
                yield return item;
            }
        }

        /// <summary>
        /// 异步为集合中的每个元素生成 ID 并赋值（立即执行）。
        /// </summary>
        public async ValueTask<IReadOnlyList<T>> ApplyIdsAsync(IIdGenerator generator,
            Action<T, string> idSetter,
            int length = 32,
            string prefix = null)
        {
            var result = new List<T>();
            await foreach (var item in source.WithIdsAsync(generator, idSetter, length, prefix))
            {
                result.Add(item);
            }
            return result;
        }
    }

    #endregion

    [Conditional("DEBUG")]
    private static void DebugWarnIfUnsorted()
    {
        Debug.WriteLine("警告：未排序的数据可能导致生成的带时间戳 ID 在逻辑顺序上不连续。");
    }
}