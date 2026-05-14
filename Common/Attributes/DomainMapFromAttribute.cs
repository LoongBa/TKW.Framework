using System;

namespace TKW.Framework.Attributes;

/// <summary>
/// 标记目标类自动生成高性能映射代码（支持 Source Generator）
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class DomainMapFromAttribute : Attribute
{
    /// <summary>
    /// 声明该类需要从哪些源类型自动生成映射逻辑。
    /// 如果不传参数，默认生成自身的映射逻辑（用于深拷贝/Clone）。
    /// </summary>
    /// <param name="sourceTypes">源类型列表</param>
    public DomainMapFromAttribute(params Type[] sourceTypes)
    {
    }
}