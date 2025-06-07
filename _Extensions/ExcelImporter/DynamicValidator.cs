using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using FluentValidation;
using TKW.Framework.Common.Validation;

namespace TKWF.ExcelImporter;

/// <summary>
/// 动态验证器（基于模板配置）
/// </summary>
public partial class DynamicValidator<T> : AbstractValidator<T>
{
    private readonly IExpressionEvaluator _ExpressionEvaluator;

    /// <summary>
    /// 构造函数，初始化动态验证器
    /// </summary>
    /// <param name="template">Excel模板配置</param>
    /// <param name="expressionEvaluator">表达式求值器</param>
    public DynamicValidator(ExcelTemplateConfiguration template, IExpressionEvaluator expressionEvaluator)
    {
        _ExpressionEvaluator = expressionEvaluator;

        // 应用行级验证规则
        ApplyRowValidations(template);
    }

    /// <summary>
    /// 应用行级验证规则
    /// </summary>
    /// <param name="template">Excel模板配置</param>
    private void ApplyRowValidations(ExcelTemplateConfiguration template)
    {
        foreach (var validation in template.RowValidations)
        {
            // 解析行级验证规则：格式为 "条件?表达式" 或 "表达式"
            var parts = validation.Split('?');
            string condition = null;
            var expression = parts[0];

            if (parts.Length > 1)
            {
                condition = parts[0].Trim();
                expression = parts[1].Trim();
            }

            // 添加行级验证
            RuleFor(x => x)
                .Must(instance => EvaluateRowValidation(instance, condition, expression))
                .WithMessage($"行验证失败: {validation}");
        }
    }

    /*
    /// <summary>
    /// 应用字段级验证规则
    /// </summary>
    /// <param name="template">Excel模板配置</param>
    private void ApplyFieldValidations(ExcelTemplateConfiguration template)
    {
        foreach (var validation in template.FieldValidations)
        {
            // 解析验证规则：格式为 "字段名:规则1(参数),规则2(参数)"
            var parts = validation.Split(':');
            if (parts.Length != 2) continue;

            var fieldName = parts[0].Trim();
            var rules = parts[1].Split(',').Select(r => r.Trim()).ToList();

            var propertyInfo = typeof(T).GetProperty(fieldName);
            if (propertyInfo == null) continue;

            // 修正：直接调用 RuleFor，无需 validator 变量
            var ruleBuilder = CreateRuleBuilder(propertyInfo);
            if (ruleBuilder == null) continue;

            foreach (var rule in rules)
            {
                ApplySingleFieldRule(ruleBuilder, rule, propertyInfo);
            }
        }
    }*/

    /// <summary>
    /// 解析并应用单个验证规则
    /// </summary>
    /// <param name="ruleBuilder">规则构建器</param>
    /// <param name="rule">验证规则</param>
    /// <param name="property">属性信息</param>
    private static void ApplySingleFieldRule(IRuleBuilderInitial<T, object> ruleBuilder, string rule, PropertyInfo property)
    {
        // 解析并应用单个验证规则
        if (rule.StartsWith("Required"))
        {
            ruleBuilder.NotNull().WithMessage("{PropertyName}不能为空");
        }
        else if (rule.StartsWith("MaxLength") && property.PropertyType == typeof(string))
        {
            if (ruleBuilder is IRuleBuilderInitial<T, string> stringRuleBuilder)
            {
                var length = int.Parse(MaxLengthRegex().Match(rule).Groups[1].Value);
                stringRuleBuilder.MaximumLength(length).WithMessage("{PropertyName}长度不能超过{MaxLength}");
            }
        }
        else if (rule.StartsWith("Range") && typeof(IComparable).IsAssignableFrom(property.PropertyType))
        {
            var min = Convert.ChangeType(RangeMinRegex().Match(rule).Groups[1].Value, property.PropertyType);
            var max = Convert.ChangeType(RangeMaxRegex().Match(rule).Groups[1].Value, property.PropertyType);

            // 通过反射调用 InclusiveBetween 方法，并保留返回值
            var inclusiveBetweenMethod = typeof(DefaultValidatorExtensions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == "InclusiveBetween" && m.GetParameters().Length == 3);

            var genericMethod = inclusiveBetweenMethod.MakeGenericMethod(typeof(T), property.PropertyType);
            var ruleBuilderWithCondition = genericMethod.Invoke(null, [ruleBuilder, min, max]);

            // 设置错误消息（通过反射调用 WithMessage 方法）
            var withMessageMethod = ruleBuilderWithCondition?.GetType()
                .GetMethod("WithMessage", [typeof(string)]);

            withMessageMethod?.Invoke(ruleBuilderWithCondition, ["{PropertyName}必须在{From}到{To}之间"]);
        }
        else if (rule == "Email" && property.PropertyType == typeof(string))
        {
            if (ruleBuilder is IRuleBuilderInitial<T, string> stringRuleBuilder)
            {
                stringRuleBuilder.EmailAddress().WithMessage("{PropertyName}不是有效的邮箱地址");
            }
        }
        else if (rule == "Date" && (property.PropertyType == typeof(DateTime) || property.PropertyType == typeof(DateTime?)))
        {
            ruleBuilder.Must(x => x is DateTime || x is string s && DateTime.TryParse(s, out _))
                .WithMessage("{PropertyName}不是有效的日期格式");
        }
        // 可扩展更多验证规则...
    }

    /// <summary>
    /// 验证电子邮件地址是否有效
    /// </summary>
    public static bool IsValidEmail(string email)
    {
        return email.IsValidEmail();
    }

    /// <summary>
    /// 评估行级验证
    /// </summary>
    /// <param name="instance">实例对象</param>
    /// <param name="condition">条件表达式</param>
    /// <param name="expression">验证表达式</param>
    /// <returns>是否通过验证</returns>
    private bool EvaluateRowValidation(object instance, string condition, string expression)
    {
        // 如果有条件，先检查条件是否满足
        if (!string.IsNullOrEmpty(condition) && !_ExpressionEvaluator.Evaluate(instance, condition))
        {
            return true; // 条件不满足，验证自动通过
        }

        // 执行表达式验证
        return _ExpressionEvaluator.Evaluate(instance, expression);
    }

    /// <summary>
    /// 创建规则构建器
    /// </summary>
    /// <param name="propertyInfo">属性信息</param>
    /// <returns>规则构建器</returns>
    private IRuleBuilderInitial<T, object> CreateRuleBuilder(PropertyInfo propertyInfo)
    {
        // 使用 Expression<Func<T, object>> 动态创建属性访问表达式
        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.Property(parameter, propertyInfo);
        var convert = Expression.Convert(property, typeof(object));
        var lambda = Expression.Lambda<Func<T, object>>(convert, parameter);

        // 直接调用 RuleFor（因为当前处于 AbstractValidator<T> 的子类中）
        return RuleFor(lambda);
    }

    [GeneratedRegex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"MaxLength\((\d+)\)", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex MaxLengthRegex();

    [GeneratedRegex(@"Range\((.*?),", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex RangeMinRegex();

    [GeneratedRegex(@",(.*?)\)", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex RangeMaxRegex();
}
