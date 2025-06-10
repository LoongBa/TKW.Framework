using TKWF.DMP.Core.Models;

namespace TKWF.DMP.Core.Interfaces;

/// <summary>
/// 统计引擎核心接口（支持多计算器并行处理、组件注册与流程控制）
/// </summary>
public interface IStatEngine
{
    #region 组件注册方法（显式注册）
    /// <summary>
    /// 注册数据加载器实例
    /// </summary>
    void RegisterLoader<T>(string name, IDataLoader<T> loader) where T : class, new();

    /// <summary>
    /// 注册预处理器实例
    /// </summary>
    void RegisterPreprocessor<T>(string name, IDataPreprocessor<T> preprocessor) where T : class;

    /// <summary>
    /// 注册数据导出器实例
    /// </summary>
    void RegisterExporter<T>(string name, IDataExporter<T> exporter) where T : class, new();
    #endregion

    #region 类型注册方法（动态加载）
    /// <summary>
    /// 注册组件类型（供动态加载使用）
    /// </summary>
    void RegisterComponentType<TInterface>(string name, Type implementationType) where TInterface : class;
    #endregion

    #region 类型查询方法（供工厂调用）
    /// <summary>
    /// 查询已注册的组件类型
    /// </summary>
    Type? GetRegisteredType<TInterface>(string name) where TInterface : class;
    #endregion

    #region 核心执行方法（支持多计算器）
    /// <summary>
    /// 执行完整数据处理流程：加载 → 预处理 → 计算（多计算器）→ 导出
    /// </summary>
    TResult Execute<T, TResult>(
        string loaderName,
        List<string> calculatorTypes,
        string exporterName)
        where T : class, new()
        where TResult : class, new();
    #endregion

    #region 配置获取方法
    /// <summary>
    /// 获取处理阶段配置（供工厂传递参数）
    /// </summary>
    ProcessingConfig GetProcessingConfig();
    #endregion
}