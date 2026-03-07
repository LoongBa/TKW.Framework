using System;
using System.Collections.Generic;

namespace TKW.Framework.Common.Entity;

#region 通用分页结果模型
/// <summary>
/// 兼容 FreeSql 分页逻辑的通用返回模型
/// </summary>
/// <typeparam name="T"></typeparam>
public class PageResult<T>
{
    /// <summary>
    /// 总记录数 (与 FreeSql CountAsync 返回类型对齐) 
    /// </summary>
    public long TotalCount { get; set; }

    /// <summary>
    /// 当前页码
    /// </summary>
    public int PageIndex { get; set; }

    /// <summary>
    /// 每页数量
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// 总页数 (自动计算)
    /// </summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    /// <summary>
    /// 列表数据
    /// </summary>
    public List<T> Data { get; set; } = [];

    /// <summary>
    /// 默认构造函数
    /// </summary>
    public PageResult() { }

    /// <summary>
    /// 快速构造函数 (用于 Service 内部实例化)
    /// </summary>
    public PageResult(long totalCount, List<T> data, int pageIndex = 1, int pageSize = 20)
    {
        TotalCount = totalCount;
        Data = data ?? [];
        PageIndex = pageIndex;
        PageSize = pageSize;
    }
}
#endregion