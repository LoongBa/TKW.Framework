using System.Collections.Generic;

namespace TKW.Framework.Common.Entity;

#region 通用分页结果模型
/// <summary>
/// GraphQL 分页返回模型
/// </summary>
/// <typeparam name="T"></typeparam>
public class PageResult<T>
{
    public int TotalCount { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public List<T> Data { get; set; } = [];
}
#endregion