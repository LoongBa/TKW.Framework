using System;

namespace TKW.Framework.Common.Entity.Interfaces;

public static class ObjectWithCopyValuesExtention
{
    public static TEntityObject Clone<TEntityObject>(this TEntityObject left)
        where TEntityObject : IObjectWithCopyValues<TEntityObject>, new()
    {
        return new TEntityObject().CopyValuesFrom(left);
    }

    public static TIEntityModel CopyValuesToNew<TIEntityModel>(this TIEntityModel left)
        where TIEntityModel : IObjectWithCopyValues<TIEntityModel>, new()
    {
        var model = new TIEntityModel();
        return left.CopyValuesTo(model);
    }

    public static TIEntityModel CopyValuesTo<TIEntityModel>(this TIEntityModel left, TIEntityModel model)
        where TIEntityModel : IObjectWithCopyValues<TIEntityModel>, new()
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        model.CopyValuesFrom(left);
        return model;
    }
}