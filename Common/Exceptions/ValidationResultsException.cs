using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace TKW.Framework.Common.Exceptions;

public class ValidationResultsException(IList<ValidationResult> results)
    : ValidationException(results.FirstOrDefault()?.ErrorMessage)
{
    public IEnumerable<ValidationResult> Results { get; } = results;
}