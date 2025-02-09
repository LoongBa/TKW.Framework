using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using TKW.Framework.Common.Entity;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Common.DataType
{
    public class Pager<T> where T : class
    {
        /// <summary>初始化 <see cref="T:System.Object" /> 类的新实例。</summary>
        public Pager(IList<T> data, uint total, uint pageIndex = 0, uint pageSize = _DefaultPageSize_, uint maxPageSize = 100)
        {
            data.AssertNotNull();
            if (pageSize == 0) pageSize = _DefaultPageSize_;

            Data = data;
            Total = total;
            PageIndex = pageIndex;
            PageSize = pageSize;


            if (total % pageSize > 0) //求模余数 > 0
                PageCount = total / pageSize + 1;
            else
                PageCount = total / pageSize;


            if (maxPageSize > 500) throw new ArgumentOutOfRangeException(nameof(maxPageSize), @"建议不要大于500");
            MaxPageSize = maxPageSize;
            if (PageSize > MaxPageSize) PageSize = MaxPageSize;
        }

        public Pager(IList<T> list)
        {
            list.AssertNotNull();
            Data = list;
            Total = (uint)list.Count;
            PageSize = Total;
            PageCount = (uint)(Total == 0 ? 0 : 1);
            PageIndex = 0;
        }

        [JsonIgnore]
        public uint MaxPageSize { get; }
        public const uint _DefaultPageSize_ = 25;

        public uint Total { get; }
        public uint PageCount { get; }
        public uint PageIndex { get; }
        public uint PageSize { get; }

        public IList<T> Data { get; }
    }

    public static class PagerExt
    {
        public static Pager<T> ToPager<T>(this IList<T> datas, uint total, uint pageIndex = 0, uint pageSize = Pager<T>._DefaultPageSize_, uint maxPageSize = 100)
            where T : class
        {
            return new Pager<T>(datas, total, pageIndex, pageSize, maxPageSize);
        }
        public static Pager<T> ToPager<T>(this IList<T> datas)
            where T : class
        {
            return new Pager<T>(datas);
        }

        public static Pager<T> ToPager<T>(this IList<T> datas, Pager pager)
            where T : class
        {
            return new Pager<T>(datas, (uint)pager.TotalCount, (uint)pager.PageIndex, (uint)pager.PageSize, (uint)pager.MaxPageSize);
        }
    }
}