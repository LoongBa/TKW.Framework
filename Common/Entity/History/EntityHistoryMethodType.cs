using System.ComponentModel.DataAnnotations;

namespace TKW.Framework.Common.Entity.History
{
    public enum EntityHistoryMethodType
    {
        /// <summary>
        /// 保存全部信息
        /// </summary>
        [Display(Name = "保存全部信息")]
        All = 0,
        /// <summary>
        /// 仅保存差异
        /// </summary>
        [Display(Name = "仅保存差异")]
        Difference = 1
    }
}