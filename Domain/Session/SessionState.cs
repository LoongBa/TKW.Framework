
using System.ComponentModel.DataAnnotations;

namespace TKW.Framework.Domain.Session;

/// <summary>
/// 会话状态
/// </summary>
public enum SessionState
{
    /// <summary>
    /// 活动
    /// </summary>
    [Display(Name = "活动")]
    Alive = 1,
    /// <summary>
    /// 超时
    /// </summary>
    [Display(Name = "超时")]
    Timeout = 0,
    /// <summary>
    /// 注销
    /// </summary>
    [Display(Name = "注销")]
    Abandon = -1,
}