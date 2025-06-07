using System.Linq.Expressions;
using System.Reflection;
using FluentValidation;

namespace TKWF.ExcelImporter;

public static class FluentValidationExtensions
{
    /// <summary>
    /// 通过 PropertyInfo 创建验证规则
    /// </summary>
    public static IRuleBuilderInitial<T, object> RuleForProperty<T>(
        this AbstractValidator<T> validator,
        PropertyInfo propertyInfo)
    {
        // 创建 lambda 表达式：x => x.[PropertyName]
        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.Property(parameter, propertyInfo);
        var convert = Expression.Convert(property, typeof(object));
        var lambda = Expression.Lambda<Func<T, object>>(convert, parameter);

        // 调用原生 RuleFor 方法
        return validator.RuleFor(lambda);
    }
}