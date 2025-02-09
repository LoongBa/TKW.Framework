using System.ComponentModel.DataAnnotations;

namespace TKW.Framework.Common.Entity.History
{
    public enum EntityHistoryOperationType
    {
        [Display(Name = "创建")]
        Create = 1,
        [Display(Name = "更新")]
        Update = 2,
        [Display(Name = "删除")]
        Delete = 3,
        [Display(Name = "恢复删除")]
        Undelete = 4,
        [Display(Name = "禁用")]
        Disable = 5,
        [Display(Name = "启用")]
        Enable = 6,
        [Display(Name = "重置密码")]
        ResetPassword = 7,
    }
}