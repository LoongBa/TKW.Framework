using Microsoft.Data.SqlClient;
using TKWF.DMPCore.Interfaces;
using TKWF.DMPCore.Models;

namespace TKWF.DMPCore.Plugins.DataLoaders;
/// <summary>
/// 数据加载器实现示例 - 数据库加载
/// </summary>
public class DatabaseDataLoader : IDataLoader
{
    public string SourceType => "database";

    public IEnumerable<Dictionary<string, object>> Load(DataLoadOptions options)
    {
        if (string.IsNullOrEmpty(options.ConnectionString))
            throw new ArgumentException("数据库连接字符串不能为空");

        if (string.IsNullOrEmpty(options.Query))
            throw new ArgumentException("查询语句不能为空");

        using var connection = new SqlConnection(options.ConnectionString);
        var command = new SqlCommand(options.Query, connection);
        command.Parameters.AddWithValue("@Start", options.StartTime);
        command.Parameters.AddWithValue("@End", options.EndTime);

        connection.Open();
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            var dict = new Dictionary<string, object>(reader.FieldCount);
            for (var i = 0; i < reader.FieldCount; i++) dict[reader.GetName(i)] = reader.GetValue(i);
            yield return dict;
        }
    }
}