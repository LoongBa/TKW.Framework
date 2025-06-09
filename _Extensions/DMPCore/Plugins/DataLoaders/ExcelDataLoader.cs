using TKWF.DMP.Core.Interfaces;
using TKWF.DMP.Core.Models;

namespace TKWF.DMP.Core.Plugins.DataLoaders
{
    internal class ExcelDataLoader: IDataLoader
    {
        public string SourceType { get; }
        public IEnumerable<Dictionary<string, object>> Load(DataLoadOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
