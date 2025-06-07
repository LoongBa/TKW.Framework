using FluentValidation;

namespace TKWF.ExcelImporter;

/// <summary>
/// 验证规则解析器
/// </summary>
public interface IValidationRuleParser
{
    void ApplyRulesToValidator<T>(AbstractValidator<T> validator, ExcelTemplateConfiguration template);
}