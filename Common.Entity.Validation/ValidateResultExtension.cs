using FluentValidation.Results;
using TKW.Framework.Common.Entity.Exceptions;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Common.Validation;

public static class ValidateResultExtension
{
    public static void ThrowException(this ValidationResult left, string? entityName = null)
    {
        if (!left.AssertNotNull(nameof(left)).IsValid)
            throw new EntityValidationException(left, entityName);
    }
    public static ValidationResult RaiseEvent(this ValidationResult left, object sender, ModelValidatedEventDelegate modelValidatedEvent)
    {
        modelValidatedEvent?.Invoke(sender.AssertNotNull(nameof(sender)), new ModelValidatedEventDelegateArgs(left));
        return left;
    }
}