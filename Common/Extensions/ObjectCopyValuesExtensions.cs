using System;
using System.Collections.Generic;
using System.Linq;
using TKW.Framework.Common.DataType;

namespace TKW.Framework.Common.Extensions;

/// <summary>
/// 基于反射的对象属性值复制与比较扩展方法
/// </summary>
/*public static class ObjectCopyValuesExtensions
{
    extension<TEntity>(TEntity left)
    {
        /// <summary>
        /// 复制同名属性的值（采用反射，影响性能，不推荐使用）
        /// </summary>
        public TEntity CopySamePropertiesValue<TModel>(TModel copyFrom)
        {
            left.AssertNotNull();
            copyFrom.AssertNotNull();

            var copyFromProperties = copyFrom.GetType().GetProperties();
            foreach (var copyFromProperty in copyFromProperties)
            {
                if (!copyFromProperty.GetAccessors().Any(p => p.IsPublic)) continue;
                if (!copyFromProperty.CanRead) continue;
                //赋值
                var leftProperty = left.GetType()
                    .GetProperties()
                    .FirstOrDefault(p => p.Name.Equals(copyFromProperty.Name, StringComparison.Ordinal) && p.CanWrite);
                leftProperty?.SetValue(left, copyFromProperty.GetValue(copyFrom));
            }
            return left;
        }

        /// <summary>
        /// 比较同名属性的差异
        /// </summary>
        public IEnumerable<ValueDifference> CompareSamePropertiesDifference<TModel>(TModel comparedWith)
        {
            comparedWith.AssertNotNull();

            var differences = new List<ValueDifference>();
            var fromProperties = comparedWith.GetType().GetProperties();
            foreach (var fromProperty in fromProperties)
            {
                if (!fromProperty.GetAccessors().Any(p => p.IsPublic)) continue;
                if (!fromProperty.CanRead) continue;
                //if (fromProperty.GetCustomAttributes(typeof(JsonIgnoreAttribute), false).Any()) continue;
                //if (fromProperty.GetCustomAttributes(typeof(EntityHistoryIgnoreAttribute), false).Any()) continue;
                if (fromProperty.Name.Equals("EntityGuid", StringComparison.OrdinalIgnoreCase)) continue;
                if (fromProperty.Name.Equals("EntityId", StringComparison.OrdinalIgnoreCase)) continue;
                if (fromProperty.Name.Equals("EntityHistory", StringComparison.OrdinalIgnoreCase)) continue;

                //赋值
                var leftProperty = left.GetType()
                    .GetProperties()
                    .FirstOrDefault(p => p.Name.Equals(fromProperty.Name, StringComparison.Ordinal) && p.CanWrite);
                if (leftProperty == null) continue;

                //比较值
                var leftValue = leftProperty.GetValue(left);
                var comparedWithValue = fromProperty.GetValue(comparedWith);
                if (leftProperty.PropertyType.Name.Equals("string", StringComparison.OrdinalIgnoreCase) && Equals(leftValue, comparedWithValue))
                    continue;
                else if (leftValue == comparedWithValue) continue;
                var difference = new ValueDifference(leftProperty.Name, leftValue, comparedWithValue);
                if (difference.IsSameValue() || difference.IsSameValueString()) continue;
                //记录
                differences.Add(difference);
            }
            return differences;
        }
    }
}*/