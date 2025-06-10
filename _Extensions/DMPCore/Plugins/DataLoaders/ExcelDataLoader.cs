using TKWF.DMP.Core.Interfaces;
using TKWF.DMP.Core.Models;

namespace TKWF.DMP.Core.Plugins.DataLoaders
{
    public class ExcelDataLoader<T> : IDataLoader<T>
    where T : class, new()
    {
        public string SourceType { get; private set; } = "Excel";
        public IEnumerable<T> Load(DataLoadOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
