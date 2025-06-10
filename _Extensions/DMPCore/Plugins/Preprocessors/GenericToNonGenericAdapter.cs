using TKWF.DMP.Core.Interfaces;

namespace TKWF.DMP.Core.Plugins.Preprocessors;

public class GenericToNonGenericAdapter<T>(IDataPreprocessor<T> genericPreprocessor) : IPreprocessor
    where T : class
{
    public object Process(object data)
    {
        return genericPreprocessor.Process((IEnumerable<T>)data);
    }
}