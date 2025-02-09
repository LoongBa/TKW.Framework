using System;
using System.Collections.Generic;
using System.Linq;
using TKW.Framework.Common.DataType;
using TKW.Framework.Common.Entity.Interfaces;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Common.Entity.History
{
    public class EntityHistoryHelper<TEntity> : IEntityHistoryHelper<TEntity>
        where TEntity : IEntityHistory<TEntity>
    {
        #region Implementation of IEntityBase

        #endregion

        /// <summary>初始化 <see cref="T:System.Object" /> 类的新实例。</summary>
        public EntityHistoryHelper(TEntity entityInstance, EntityHistoryMethodType methodType = EntityHistoryMethodType.Difference)
        {
            if (entityInstance == null) throw new ArgumentNullException(nameof(entityInstance));
            EntityInstance = entityInstance;
            MethodType = methodType;
            CurrentContent = string.Empty;
            Differences = new List<ValueDifference>();
        }
        private TEntity EntityInstance { get; }

        #region Implementation of IEntityHistoryHelper<in T>

        /// <summary>
        /// 获取历史信息（差异）
        /// </summary>
        public virtual TEntity CompareForHistory(TEntity oldValue)
        {
            if (oldValue == null) throw new ArgumentNullException(nameof(oldValue));
            var differences = oldValue.CompareSamePropertiesDifference(EntityInstance);
            var list = (List<ValueDifference>)Differences;
            list.Clear();
            list.AddRange(differences.Select(difference => new ValueDifference(difference)));
            return EntityInstance;
        }

        /// <summary>
        /// 最近一次比较的差异
        /// </summary>
        public IEnumerable<ValueDifference> Differences { get; }

        /// <summary>
        /// 获取历史信息（全部）
        /// </summary>
        public virtual string CurrentVersionForHistory()
        {
            return CurrentContent = EntityInstance.ToJson();
        }

        /// <summary>
        /// 格式类型
        /// </summary>
        public EntityHistoryFormatType FormatType => EntityHistoryFormatType.JsonOnly;

        /// <summary>
        /// 比较方式类型
        /// </summary>
        public EntityHistoryMethodType MethodType { get; }

        /// <summary>
        /// 比较结果
        /// </summary>
        public string CurrentContent { get; private set; }

        /// <summary>
        /// 获得比较结果
        /// </summary>
        public string ComparedResult => MethodType == EntityHistoryMethodType.Difference
                                            ? Differences?.ToJson() ?? string.Empty
                                            : CurrentContent ?? string.Empty;
        #endregion
    }
}