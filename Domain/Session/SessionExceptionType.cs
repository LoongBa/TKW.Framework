using System.ComponentModel.DataAnnotations;

namespace TKW.Framework.Domain.Session;

public enum SessionExceptionType
{
    [Display(Name = "没有找到会话")]
    SessionNotFound = -1,

    [Display(Name = "重复的会话值")]
    DuplicatedSessionKey = -2,

    [Display(Name = "无效的键值")]
    InvalidSessionKey = -3,

    [Display(Name = "无效的会话值")]
    InvalidSessionValue = -4,

    [Display(Name = "更新会话失败")]
    UpdateSessionFailed = -5,
}