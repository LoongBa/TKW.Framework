namespace TKWF.DMP.Core.Interfaces;

/// <summary>
/// 预处理器工厂接口（负责创建 IDataPreprocessor 实例）
/// </summary>
public interface IPreprocessorFactory
{
    /// <summary>
    /// 创建指定名称的预处理器实例
    /// </summary>
    /// <typeparam name="T">输入数据类型</typeparam>
    /// <param name="preprocessorName">预处理器名称（如 "TimeFilter", "DataCleaner"）</param>
    /// <returns>IDataPreprocessor<T> 实例</returns>
    IDataPreprocessor<T> Create<T>(string preprocessorName) where T : class;
}