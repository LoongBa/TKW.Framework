namespace TKWF.DMPCore.Interfaces;
/// <summary>
/// 配置管理器接口
/// </summary>
public interface IConfigManager
{
    T GetConfig<T>(string section);
    void Validate();
}