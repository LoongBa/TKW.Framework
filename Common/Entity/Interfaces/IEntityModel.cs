using ValidationResult = FluentValidation.Results.ValidationResult;

namespace TKW.Framework.Common.Entity.Interfaces
{
    public interface IEntityModel : IEntityQueryable
    {
        //void SetDefaultValues();
        /*object ToEntity(int level = 0);
        object ToModel(int level = 0);*/

        ValidationResult ValidateModel();
    }
}