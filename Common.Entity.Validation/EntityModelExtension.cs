using TKW.Framework.Common.Validation;

namespace TKW.Framework.Common.Entity.Interfaces;

public static class EntityModelExtension
{
    public static void ValidateAndThrow<T>(this T model) 
        where T : class, IEntityModel
    {
        model.ValidateModel().ThrowException(model.GetType().Name);
    }
}