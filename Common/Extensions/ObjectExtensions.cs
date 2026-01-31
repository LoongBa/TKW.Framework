using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

//using System.Text.Json.Serialization;

namespace TKW.Framework.Common.Extensions;

public static class ObjectExtensions
{
    public static T EnsureNotNull<T>(this T left, string name = null, string message = null)
        //where T : class
    {
        name = name.HasValue() ? name : left.GetType().Name;
        if (left == null)
            throw new ArgumentNullException(
                message.HasValue() ? message : "参数 '{0}' 值不能为 null", name);
        return left;
    }
    public static string EnsureNotEmptyOrNull(this string left, string name = null, string message = null)
    {
        name = name.HasValue() ? name : left.GetType().Name;
        if (left == null)
            throw new ArgumentNullException(
                message.HasValue() ? message : "参数 '{0}' 值不能为 null", name);
        return left;
    }
    public static object EnsureNotNull(this object left, string name = null, string message = null)
    {
        name = name.HasValue() ? name : left.GetType().Name;
        if (left == null)
            throw new ArgumentNullException(
                message.HasValue() ? message : "参数 '{0}' 值不能为 null", name);
        return left;
    }
    
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