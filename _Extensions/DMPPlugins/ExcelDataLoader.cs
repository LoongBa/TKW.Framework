using TKWF.DMP.Core.Interfaces;
using TKWF.DMP.Core.Models;

namespace TKWF.DMP.Plugins
{
    public class ExcelDataLoader<T>:IDataLoader<T>
    where T : class, new()
    {
        public string SourceType => "excel";
        public IEnumerable<T> Load(DataLoadOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
