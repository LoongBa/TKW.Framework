using FluentValidation.Results;

namespace TKW.Framework.Common.Entity.Interfaces
{
    public interface IEntityValidatable
    {
        ValidationResult ValidateValues();
    }
}
