using System.ComponentModel.DataAnnotations;

namespace TKW.Framework.Common.Exceptions
{
    public enum ApplicationInitialFailedType
    {
        [Display(Name = "配置项错误", Description = "确保配置项正常")]
        WrongConfiguration = 0,
        [Display(Name = "未经处理的异常", Description = "在异常处理机制启动前出现了异常")]
        ExceptionUnhandled = 1,
        [Display(Name = "服务器初始化失败", Description = "在服务器初始化阶段出现了问题")]
        ServerInitialFailed = 2,
        [Display(Name = "领域初始化失败", Description = "在领域初始化阶段出现了问题")]
        DomainInitialFailed = 3,
        [Display(Name = "需要先进行初始化", Description = "需要调用领域的初始化方法")]
        DomainNeedtoBeInitialed = 4,
        [Display(Name = "项目异常处理程序出现异常", Description = "请检查项目异常处理代码")]
        ProjectExceptionHandler = 5
    }
}