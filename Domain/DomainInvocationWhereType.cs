using System.ComponentModel.DataAnnotations;

namespace TKW.Framework.Domain;

public enum DomainInvocationWhereType
{
    [Display(Name = "全局")]
    Global = 0,
    [Display(Name = "控制器")] 
    Controller = 1,
    [Display(Name = "方法")]
    Method = 2,
}