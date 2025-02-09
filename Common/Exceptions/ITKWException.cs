using System;

namespace TKW.Framework.Common.Exceptions
{
    public interface ITKWException
    {
        /*
                /// <summary>
                /// 自定义异常的 Type 的 String 版本
                /// </summary>
                /// <returns></returns>
                string TypeString { get; }
                /// <summary>
                /// 自定义异常的 Type 的 Name（在 Type 枚举定义 [Display(Name="")]）
                /// </summary>
                string TypeName { get; }
        */

        /// <summary>
        /// 自定义的类型（基类）
        /// </summary>
        Enum ErrorType { get; }
    }
}
