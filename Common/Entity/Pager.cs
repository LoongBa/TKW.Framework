using System.Collections.Generic;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Common.Entity
{
    public class PagedResult<T>
    {
        public PagedResult(Pager pager, IList<T> resultList)
        {
            Pager = pager.AssertNotNull(nameof(pager));
            ResultList = resultList.AssertNotNull(nameof(resultList));
        }

        public Pager Pager { get; }
        public IList<T> ResultList { get; }
    }
    /// <summary>
    /// 列表数据分页
    /// </summary>
    public class Pager
    {
        private const int Default_PageSize = 25;
        private const int Max_PageSize = 500;

        /// <summary>
        /// 默认每页记录条数50条
        /// </summary>
        public int DefaultPageSize { get; }

        /// <summary>
        /// 最大每页记录条数300条
        /// </summary>
        public int MaxPageSize { get; }

        /// <summary>
        /// 初始化 <see cref="T:System.Object" /> 类的新实例。
        /// </summary>
        public Pager(int pageSize, int pageNumber, int defaultPageSize = Default_PageSize, int maxPageSize = Max_PageSize)
        {
            DefaultPageSize = defaultPageSize;
            if (DefaultPageSize <= 0) DefaultPageSize = Default_PageSize;
            MaxPageSize = maxPageSize;
            if (MaxPageSize <= 0) MaxPageSize = Max_PageSize;

            if (pageSize <= 0)
                PageSize = DefaultPageSize;
            else if (pageSize > MaxPageSize)
                PageSize = MaxPageSize;
            else
                PageSize = pageSize;

            PageNumber = pageNumber < 1 ? 1 : pageNumber;
        }

        public static Pager New(int take = Default_PageSize, int skip = 0)
        {
            if (take <= 0) take = Default_PageSize;
            if (skip <= 0) skip = 0;

            int pageNum;
            if (skip % take > 0)
                pageNum = skip / take + 1;
            else
                pageNum = skip / take;

            return new Pager(take, pageNum);
        }

        /// <summary>
        /// 每页记录条数
        /// </summary>
        public int PageSize { get; }

        /// <summary>
        /// 页索引（Base 0）
        /// </summary>
        public int PageIndex => PageNumber - 1;

        /// <summary>
        /// 页号（Base 1）
        /// </summary>
        public int PageNumber { get; }

        public int Skip => PageNumber * PageSize;

        /// <summary>
        /// 总数据条数
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// 分页数（根据 TotalCount 和 PageSize 动态计算）
        /// </summary>
        public int ComputePageCount() => TotalCount % PageSize > 0
            ? TotalCount / PageSize + 1
            : TotalCount / PageSize;
    }
}
