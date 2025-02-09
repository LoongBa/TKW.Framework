using System.Collections.Generic;
using TKW.Framework.Common.DataType;
using TKW.Framework.Common.Entity.History;

namespace TKW.Framework.Common.Entity.Interfaces
{
    public interface IEntityHistoryHelper<T>
    {
        /// <summary>
        /// 获取历史信息（差异）
        /// </summary>
        T CompareForHistory(T oldValue);
        /// <summary>
        /// 获取历史信息（全部）
        /// </summary>
        string CurrentVersionForHistory();
        /// <summary>
        /// 获得比较结果
        /// </summary>
        string ComparedResult { get; }
        /// <summary>
        /// 比较结果
        /// </summary>
        string CurrentContent { get; }
        /// <summary>
        /// 最近一次比较的差异
        /// </summary>
        IEnumerable<ValueDifference> Differences { get; }
        /// <summary>
        /// 格式类型
        /// </summary>
        EntityHistoryFormatType FormatType { get; }
        /// <summary>
        /// 比较方式类型
        /// </summary>
        EntityHistoryMethodType MethodType { get; }
    }
}