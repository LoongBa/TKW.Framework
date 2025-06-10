namespace TKWF.DMP.Core.Interfaces;

public class PreprocessorAdapter<T>(IPreprocessor innerPreprocessor) : IDataPreprocessor<T>
    where T : class
{
    public IEnumerable<T> Process(IEnumerable<T> data)
    {
        return (IEnumerable<T>)innerPreprocessor.Process(data);
    }
}