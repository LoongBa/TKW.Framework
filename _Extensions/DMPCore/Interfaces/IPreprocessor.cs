namespace TKWF.DMPCore.Interfaces;
/// <summary>
/// 预处理插件接口
/// </summary>
public interface IPreprocessor
{
    string Name { get; }
    void Process(Dictionary<string, object> dataItem);
}