using FluentValidation.Results;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Common.Validation;

public class ModelValidatedEventDelegateArgs
{
    public ModelValidatedEventDelegateArgs(ValidationResult result)
    {
        Result = result.AssertNotNull(nameof(result));
    }

    public ValidationResult Result { get; }
}