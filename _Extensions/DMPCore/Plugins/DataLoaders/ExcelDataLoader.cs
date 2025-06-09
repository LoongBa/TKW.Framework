using TKWF.DMPCore.Interfaces;
using TKWF.DMPCore.Models;

namespace TKWF.DMPCore.Plugins.DataLoaders
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
