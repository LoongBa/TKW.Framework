using FluentValidation.Results;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Common.Entity.Exceptions
{
    /// <summary>
    /// 实体验证异常
    /// </summary>
    public class EntityValidationException : Exception
    {
        public ValidationResult Result { get; } = new ValidationResult();

        public string EntityName { get; } = string.Empty;

        public EntityValidationException(ValidationResult validationResult, string? entityName = null) : this()
        {
            EntityName = entityName.HasNoValueToNull();
            Result = validationResult.AssertNotNull(nameof(validationResult));
        }
        public EntityValidationException()
        {
        }
    }
}
