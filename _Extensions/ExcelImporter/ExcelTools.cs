using System.Collections.Specialized;
using System.Data;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using ExcelDataReader;
using TKW.Framework.Common.Extensions;

namespace TKWF.ExcelImporter;

public class ExcelTools
{
    /// <summary>
    /// 从 Excel 导入数据
    /// </summary>
    /// <param name="filename">文件名</param>
    /// <param name="columnMapping">列映射关系</param>
    /// <param name="otherColumnsMappingName">其他列映射属性名</param>
    /// <param name="sheetIndex">Sheet 索引（从0开始，默认为 0）</param>
    /// <returns>日志数据列表</returns>
    /// <exception cref="FileNotFoundException">文件不存在</exception>
    public static async Task<IEnumerable<dynamic>> ImportDynamicObjectFromExcel(string filename, StringDictionary? columnMapping = null, string? otherColumnsMappingName = null, int sheetIndex = 0)
    {
        // 检查文件名参数
        filename.EnsureHasValue().TrimSelf();
        if (!File.Exists(filename))
            throw new FileNotFoundException($"文件不存在：{filename}");

        var records = new List<dynamic>();
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        await using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read);
        using var reader = filename.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
            ? ExcelReaderFactory.CreateCsvReader(stream)
            : ExcelReaderFactory.CreateReader(stream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration()
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration()
            {
                UseHeaderRow = true
            }
        });
        if (sheetIndex >= dataSet.Tables.Count)
            sheetIndex = 0; // 索引超出范围
        var dataTable = dataSet.Tables[sheetIndex];
        var columnIndexMapping = new Dictionary<string, int>();

        // 构建列索引映射
        foreach (DataColumn column in dataTable.Columns)
        {
            var propertyName = columnMapping?.ContainsKey(column.ColumnName) == true
                ? columnMapping[column.ColumnName]!
                : column.ColumnName; // 如果 columnMapping 为空或不包含列名，则使用列名作为属性名
            if (columnMapping?.ContainsKey(column.ColumnName) == true || !string.IsNullOrEmpty(otherColumnsMappingName))
                columnIndexMapping[propertyName] = column.Ordinal;
        }

        // 读取数据行并设置属性
        foreach (DataRow row in dataTable.Rows)
        {
            var expandoObject = new System.Dynamic.ExpandoObject() as IDictionary<string, object?>;
            var json = new JsonObject();

            foreach (var mapping in columnIndexMapping)
            {
                var value = row[mapping.Value];
                expandoObject[mapping.Key] = value == DBNull.Value ? null : value;
            }
            if (!string.IsNullOrEmpty(otherColumnsMappingName))
            {
                // 将未映射的列添加到JSON对象
                foreach (DataColumn column in row.Table.Columns)
                {
                    if (!columnIndexMapping.ContainsValue(column.Ordinal))
                    {
                        var value = row[column];
                        json[column.ColumnName] = value.ToString();
                    }
                }
                expandoObject[otherColumnsMappingName] = json;
            }
            records.Add(expandoObject);
        }

        return records;
    }

    /// <summary>
    /// 从 Excel 导入数据
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="filename">文件名</param>
    /// <param name="columnMapping">列映射关系</param>
    /// <param name="otherColumnsMappingName">其他列映射属性名</param>
    /// <param name="sheetIndex">Sheet 索引（从0开始，默认为 0）</param>
    /// <returns>日志数据列表</returns>
    /// <exception cref="FileNotFoundException">文件不存在</exception>
    public static async Task<IEnumerable<T>> ImportDataFromExcel<T>(string filename, StringDictionary columnMapping, string? otherColumnsMappingName = null, int sheetIndex = 0)
        where T : new()
    {
        // 不能忽略的参数
        columnMapping.AssertNotNull();
        // 检查文件名参数
        filename.EnsureHasValue().TrimSelf();
        if (!File.Exists(filename))
            throw new FileNotFoundException($"文件不存在：{filename}");

        // 验证其它列映射属性名参数
        PropertyInfo? otherColumnProperty = null;
        if (!string.IsNullOrEmpty(otherColumnsMappingName))
        {
            otherColumnProperty = typeof(T).GetProperty(otherColumnsMappingName);
            if (otherColumnProperty == null)
                Debug.WriteLine($"对应的类 {nameof(T)} 不存在对应属性：'{otherColumnsMappingName}'");
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        await using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read);
        var reader = filename.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
            ? ExcelReaderFactory.CreateCsvReader(stream)
            : ExcelReaderFactory.CreateReader(stream);

        var records = new List<T>();
        using (reader)
        {
            var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration()
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration()
                {
                    UseHeaderRow = true
                }
            });

            if (sheetIndex >= dataSet.Tables.Count)
                sheetIndex = 0; // 索引超出范围
            var dataTable = dataSet.Tables[sheetIndex];
            var columnIndexMapping = new Dictionary<string, int>();
            var propertyCache = new Dictionary<string, System.Reflection.PropertyInfo>();

            // 构建列索引映射和属性缓存
            foreach (DataColumn column in dataTable.Columns)
            {
                if (columnMapping != null && columnMapping.ContainsKey(column.ColumnName))
                {
                    var propertyName = columnMapping[column.ColumnName]!;
                    columnIndexMapping[propertyName] = column.Ordinal;

                    var propertyInfo = typeof(T).GetProperty(propertyName);
                    if (propertyInfo != null)
                        propertyCache[propertyName] = propertyInfo;
                }
            }

            // 读取数据行并设置属性
            foreach (DataRow row in dataTable.Rows)
            {
                var record = new T();
                var json = new JsonObject();
                SetProperties(row, columnIndexMapping, propertyCache, record, json);
                if (otherColumnProperty != null)
                {
                    otherColumnProperty.SetValue(record, json.ToJsonString(new JsonSerializerOptions
                    {
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                        WriteIndented = true
                    }));
                }
                records.Add(record);
            }

            return records;
        }
    }

    /// <summary>
    /// 设置对象的属性值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="row">数据行</param>
    /// <param name="columnIndexMapping">列索引映射</param>
    /// <param name="propertyCache">属性缓存</param>
    /// <param name="record">对象实例</param>
    /// <param name="json">其他列的JSON对象</param>
    private static void SetProperties<T>(DataRow row, Dictionary<string, int> columnIndexMapping, Dictionary<string, System.Reflection.PropertyInfo> propertyCache, T record, JsonObject json)
    {
        foreach (var mapping in columnIndexMapping)
        {
            if (propertyCache.TryGetValue(mapping.Key, out var property))
            {
                var value = row[mapping.Value];
                if (value == DBNull.Value || string.IsNullOrEmpty(value.ToString()))
                {
                    SetDefaultValue(property, record);
                }
                else
                {
                    SetPropertyValue(property, record, value);
                }
            }
        }

        // 将未映射的列添加到JSON对象
        foreach (DataColumn column in row.Table.Columns)
        {
            if (!columnIndexMapping.ContainsValue(column.Ordinal))
            {
                var value = row[column];
                json[column.ColumnName] = value.ToString();
            }
        }
    }

    /// <summary>
    /// 设置属性的默认值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="property">属性信息</param>
    /// <param name="record">对象实例</param>
    private static void SetDefaultValue<T>(System.Reflection.PropertyInfo property, T record)
    {
        if (property.PropertyType == typeof(int))
        {
            property.SetValue(record, 0);
        }
        else if (property.PropertyType == typeof(decimal))
        {
            property.SetValue(record, 0m);
        }
        else if (property.PropertyType == typeof(DateTime))
        {
            property.SetValue(record, DateTime.MinValue);
        }
        else if (property.PropertyType == typeof(DateTime?))
        {
            property.SetValue(record, null);
        }
        else
        {
            property.SetValue(record, string.Empty);
        }
    }

    /// <summary>
    /// 设置属性的值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="property">属性信息</param>
    /// <param name="record">对象实例</param>
    /// <param name="value">属性值</param>
    private static void SetPropertyValue<T>(System.Reflection.PropertyInfo property, T record, object value)
    {
        if (property.PropertyType == typeof(int))
        {
            property.SetValue(record, Convert.ToInt32(value));
        }
        else if (property.PropertyType == typeof(decimal))
        {
            property.SetValue(record, Convert.ToDecimal(value));
        }
        else if (property.PropertyType == typeof(DateTime))
        {
            property.SetValue(record, Convert.ToDateTime(value));
        }
        else if (property.PropertyType == typeof(DateTime?))
        {
            if (value is string stringValue && string.IsNullOrEmpty(stringValue))
                property.SetValue(record, null);
            else
                property.SetValue(record, Convert.ToDateTime(value));
        }
        else
        {
            property.SetValue(record, value.ToString());
        }
    }
}