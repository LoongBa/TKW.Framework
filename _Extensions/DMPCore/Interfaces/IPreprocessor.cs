namespace TKWF.DMP.Core.Interfaces;
/// <summary>
/// 预处理插件接口
/// </summary>
/// <remarks>非泛型接口（可选，用于兼容旧代码）</remarks>
public interface IPreprocessor
{
    object Process(object data);
}
