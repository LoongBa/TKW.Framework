using System;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Common.Tools
{
    public static class EnumHelper
    {
        /// <summary>
        /// 根据枚举类型的 DisplayName 返回对应的类型
        /// </summary>
        public static T ParseEnumValueByDisplay<T>(string enumValueString)
        {
            if (string.IsNullOrWhiteSpace(enumValueString))
                throw new ArgumentException(
                    "Value cannot be null or whitespace.",
                    nameof(enumValueString));

            var type = typeof(T);
            var names = Enum.GetNames(type);
            foreach (var name in names)
            {
                var enumValue = Enum.Parse(type, name);
                if (enumValueString.Equals(enumValue.GetDisplayAttribute()?.Name, StringComparison.OrdinalIgnoreCase))
                    return (T)enumValue;
            }
            throw new NotSupportedException($"{type.Name} 尚不支持枚举值：'{enumValueString}'");
        }

        /// <summary>
        /// 将字符串转换成指定枚举类型的值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="enumValueString"></param>
        /// <returns></returns>
        public static T Parse<T>(string enumValueString) where T : struct
        {
            return (T)Enum.Parse(typeof(T), enumValueString);
        }

        /// <summary>
        /// 将字符串转换成指定枚举类型的值，如转换不成功，返回指定的默认值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="enumValueString"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public static T Parse<T>(string enumValueString, T defaultValue) where T : struct
        {
            T t;
            if (Enum.TryParse(enumValueString, out t))
            {
                return t;
            }
            return defaultValue;
        }
    }
}
