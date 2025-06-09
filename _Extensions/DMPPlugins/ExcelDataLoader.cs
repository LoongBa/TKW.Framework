using TKWF.DMP.Core.Interfaces;
using TKWF.DMP.Core.Models;

namespace TKWF.DMP.Plugins
{
    public class ExcelDataLoader:IDataLoader
    {
        public string SourceType => "excel";
        public IEnumerable<Dictionary<string, object>> Load(DataLoadOptions options)
        {
            throw new NotImplementedException();
            /*if (string.IsNullOrEmpty(options.FilePath))
                throw new ArgumentException("Excel文件路径不能为空");
            if (string.IsNullOrEmpty(options.SheetName))
                throw new ArgumentException("工作表名称不能为空");
            return ExcelTools.ImportDataFromExcel(options.FilePath, options.SheetName, options.ColumnMapping).Result;*/
        }
    }
}
