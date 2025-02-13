using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using TKW.Framework.Common.DataType;
//using System.Text.Json.Serialization;

namespace TKW.Framework.Common.Extensions
{
    public static class ObjectExtensions
    {
        public static T AssertNotNull<T>(this T left, string name = null, string message = null)
            //where T : class
        {
            name = name.HasValue() ? name : left.GetType().Name;
            if (left == null)
                throw new ArgumentNullException(
                    message.HasValue() ? message : "参数 '{0}' 值不能为 null", name);
            return left;
        }
        public static object AssertNotNull(this object left, string name = null, string message = null)
        {
            name = name.HasValue() ? name : left.GetType().Name;
            if (left == null)
                throw new ArgumentNullException(
                    message.HasValue() ? message : "参数 '{0}' 值不能为 null", name);
            return left;
        }

        #region 反射相关

        /// <summary>
        /// 复制同名属性的值（采用反射，影响性能，不推荐使用）
        /// </summary>
        public static TEntity CopySamePropertiesValue<TEntity, TModel>(this TEntity left, TModel copyFrom)
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
        public static IEnumerable<ValueDifference> CompareSamePropertiesDifference<TEntity, TModel>(this TEntity left, TModel comparedWith)
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

        #endregion

        #region Attribute 相关

        /// <summary>
        /// 获取指定对象/属性的 System.ComponentModel.DataAnnotations.DisplayAttribute
        /// </summary>
        /// <see cref="DisplayAttribute"/>
        public static DisplayAttribute GetDisplayAttribute<TEnum>(this TEnum left)
        {
            var type = left.GetType();
            if (type.IsEnum)
                return type.GetMember(left.ToString()).FirstOrDefault()?.GetCustomAttributes(typeof(DisplayAttribute), false).FirstOrDefault() as DisplayAttribute;
            return type.GetCustomAttributes().FirstOrDefault(a => a is DisplayAttribute) as DisplayAttribute;
        }

        #endregion
    }
}