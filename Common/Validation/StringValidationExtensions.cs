using FluentValidation;

namespace TKW.Framework.Common.Validation;

public static class StringValidationExtensions
{
    private static readonly InlineValidator<string> EmailValidator = new InlineValidator<string>();

    /// <summary>
    /// 验证电子邮件地址是否有效
    /// </summary>
    public static bool IsValidEmail(this string email)
    {
        return !string.IsNullOrEmpty(email) && EmailValidator.Validate(email).IsValid;
    }
}